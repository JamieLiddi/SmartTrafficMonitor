using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public static class CsvImportService
    {
        // Set these folders in appsettings or environment and pass in here from the hosted service.
        public static int ImportFromFolders(ApplicationDbContext context, IAuditLogService audit, string? pedestrianFolder, string? vehicleFolder, string? cyclistFolder)
        {
            var totalImported = 0;

            totalImported += ImportFolder(context, audit, pedestrianFolder, "Pedestrian");
            totalImported += ImportFolder(context, audit, vehicleFolder, "Vehicle");
            totalImported += ImportFolder(context, audit, cyclistFolder, "Cyclist");

            return totalImported;
        }

        private static int ImportFolder(ApplicationDbContext context, IAuditLogService audit, string? folder, string movementType)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return 0;

            var files = Directory.GetFiles(folder, "*.csv").OrderBy(x => x).ToList();
            if (files.Count == 0) return 0;

            var imported = 0;

            foreach (var file in files)
            {
                try
                {
                    imported += ImportFile(context, file, movementType);
                }
                catch (Exception ex)
                {
                    audit.Log("csv_import", $"failed file={Path.GetFileName(file)} err={ex.Message}", false, null, null);
                }
            }

            if (imported > 0)
            {
                audit.Log("csv_import", $"imported movement={movementType} rows={imported}", true, null, null);
            }

            return imported;
        }

        private static int ImportFile(ApplicationDbContext context, string filePath, string movementType)
        {
            // Extract SensorId from filename if possible (eg = sensor_12_*.csv)
            var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            var slug = ExtractSensorSlug(fileNameNoExt);
            var sensorId = GetOrCreateSensorId(context, slug); ?? 0;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length <= 1) return 0;

            // existing keys to prevent duplicates 
            var existingKeys = new HashSet<string>(
                context.TrafficDatas
                    .Where(t => t.SensorId == sensorId || sensorId == 0)
                    .Select(t => $"{t.SensorId}|{t.Timestamp:O}|{t.MovementType}")
                    .ToList()
            );

            var toInsert = new List<TrafficData>();

            // Expecting header: timestamp, footCount, vehicleCount (plus optional extra columns we ignore)
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = SafeSplit(lines[i]);
                if (cols.Count < 3) continue;

                if (!TryParseDate(cols[0], out var tsUtc))
                    continue;

                var foot = TryParseInt(cols.ElementAtOrDefault(1));
                var veh = TryParseInt(cols.ElementAtOrDefault(2));

                if (foot < 0) foot = 0;
                if (veh < 0) veh = 0;

                var season = GetSeason(tsUtc);

                var row = new TrafficData
                {
                    SensorId = sensorId == 0 ? 1 : sensorId,
                    Timestamp = DateTime.SpecifyKind(tsUtc, DateTimeKind.Utc),
                    MovementType = movementType,
                    Direction = "N", // placeholder if not in the CSV
                    Season = season,
                    FootTrafficCount = foot,
                    VehicleCount = veh,
                    PublicTransportRef = false,
                    VuScheduleRef = false
                };

                var key = $"{row.SensorId}|{row.Timestamp:O}|{row.MovementType}";
                if (existingKeys.Contains(key))
                    continue;

                existingKeys.Add(key);
                toInsert.Add(row);
            }

            if (toInsert.Count == 0) return 0;

            context.TrafficDatas.AddRange(toInsert);
            context.SaveChanges();

            return toInsert.Count;
        }

        private static List<string> SafeSplit(string line)
        {
            // simple CSV split 
            return line.Split(',').Select(x => x.Trim()).ToList();
        }

        private static bool TryParseDate(string raw, out DateTime dt)
        {
            // Accept multiple formats
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy H:mm:ss",
                "dd/MM/yyyy"
            };

            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return true;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return true;

            dt = default(DateTime);
            return false;
        }

        private static int TryParseInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            return int.TryParse(raw, out var v) ? v : 0;
        }

        private static string GetSeason(DateTime tsUtc)
        {
            // AU seasons
            var m = tsUtc.Month;
            if (m == 12 || m == 1 || m == 2) return "Summer";
            if (m >= 3 && m <= 5) return "Autumn";
            if (m >= 6 && m <= 8) return "Winter";
            return "Spring";
        }

        private static int? TryExtractSensorId(string fileNameNoExt)
        {
            // pull the first number found
            var digits = new string(fileNameNoExt.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits)) return null;

            if (int.TryParse(digits, out var id))
                return id;

            return null;
        }

        private static string ExtractSensorSlug(string fileNameNoExt)
        {
            // Example:
            // device_mcc---cyclist---video-analytics---footscray-library-car-park__variable_cyclistcount__aggregation_raw
            var marker = "video-analytics---";
            var idx = fileNameNoExt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
            {
                var after = fileNameNoExt[(idx + marker.Length)..];
                var end = after.IndexOf("__variable", StringComparison.OrdinalIgnoreCase);
                if (end > 0)
                    return after[..end].Trim().ToLowerInvariant();
            }

            // Fallback: try last --- segment
            var parts = fileNameNoExt.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
            return parts.LastOrDefault()?.Trim().ToLowerInvariant() ?? "unknown-sensor";
        }

        private static int GetOrCreateSensorId(ApplicationDbContext context, string slug)
        {
            var existing = context.SensorLocations.FirstOrDefault(s => s.SensorSlug == slug);
            if (existing != null) return existing.SensorId;

            var created = new SensorLocation
            {
                SensorSlug = slug,
                Latitude = null,
                Longitude = null,
                Zone = ""
            };

            context.SensorLocations.Add(created);
            context.SaveChanges();
            return created.SensorId;
        }
    }
}