using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public static class CsvImportService
    {
        public static int ImportFromFolders(
            ApplicationDbContext context,
            IAuditLogService audit,
            string? pedestrianFolder,
            string? vehicleFolder,
            string? cyclistFolder)
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
                audit.Log("csv_import", $"imported movement={movementType} rows={imported}", true, null, null);

            return imported;
        }

        private static int ImportFile(ApplicationDbContext context, string filePath, string movementType)
        {
            var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            var slug = ExtractSensorSlug(fileNameNoExt);

            EnsureSensorLocationRow(context, slug);

            var lines = File.ReadAllLines(filePath);
            if (lines.Length <= 1) return 0;

            // Header-based column detection
            var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();

            int idxDate = header.IndexOf("date");
            int idxTimestamp = header.IndexOf("timestamp"); // sometimes epoch ms
            int idxValue = header.IndexOf("value");

            if (idxValue < 0)
                return 0; // can't import without value

            // Pull existing keys for this sensor+movement to prevent duplicates
            var existingKeys = new HashSet<string>(
                context.TrafficDatas
                    .AsNoTracking()
                    .Where(t => t.SensorId == slug && t.MovementType == movementType)
                    .Select(t => $"{t.SensorId}|{t.Timestamp:O}|{t.MovementType}")
                    .ToList()
            );

            var toInsert = new List<TrafficData>();

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = SplitCsvLine(lines[i]);
                if (cols.Count <= idxValue) continue;

                if (!TryParseTimestamp(cols, idxDate, idxTimestamp, out var tsUtc))
                    continue;

                var value = TryParseValueToInt(cols.ElementAtOrDefault(idxValue));
                if (value < 0) value = 0;

                var season = GetSeason(tsUtc);

                var row = new TrafficData
                {
                    SensorId = slug,
                    Timestamp = DateTime.SpecifyKind(tsUtc, DateTimeKind.Utc),
                    MovementType = movementType,
                    Direction = "N",
                    Season = season,

                    // Map value into the right column
                    FootTrafficCount = (movementType == "Vehicle") ? 0 : value,
                    VehicleCount = (movementType == "Vehicle") ? value : 0,

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

        private static List<string> SplitCsvLine(string line)
        {
            // Your files are simple CSV (no quoted commas in fields in the sample),
            // so a basic split is OK.
            return line.Split(',').Select(x => x.Trim()).ToList();
        }

        private static bool TryParseTimestamp(List<string> cols, int idxDate, int idxTimestamp, out DateTime utc)
        {
            // Prefer the "date" column (it has timezone like +11:00)
            if (idxDate >= 0 && idxDate < cols.Count)
            {
                var raw = cols[idxDate];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    {
                        utc = dto.UtcDateTime;
                        return true;
                    }
                }
            }

            // Fallback: "timestamp" column might be epoch ms
            if (idxTimestamp >= 0 && idxTimestamp < cols.Count)
            {
                var raw = cols[idxTimestamp];
                if (long.TryParse(raw, out var ms) && ms > 0)
                {
                    utc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                    return true;
                }
            }

            utc = default;
            return false;
        }

        private static int TryParseValueToInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            // Often looks like "8014.0"
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return (int)Math.Round(d);

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;

            return 0;
        }

        private static string GetSeason(DateTime tsUtc)
        {
            var m = tsUtc.Month;
            if (m == 12 || m == 1 || m == 2) return "Summer";
            if (m >= 3 && m <= 5) return "Autumn";
            if (m >= 6 && m <= 8) return "Winter";
            return "Spring";
        }

        private static string ExtractSensorSlug(string fileNameNoExt)
        {
            var marker = "video-analytics---";
            var idx = fileNameNoExt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
            {
                var after = fileNameNoExt[(idx + marker.Length)..];
                var end = after.IndexOf("__variable", StringComparison.OrdinalIgnoreCase);
                if (end > 0)
                    return after[..end].Trim().ToLowerInvariant();
            }

            var parts = fileNameNoExt.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
            return parts.LastOrDefault()?.Trim().ToLowerInvariant() ?? "unknown-sensor";
        }

        private static void EnsureSensorLocationRow(ApplicationDbContext context, string slug)
        {
            var exists = context.SensorLocations.Any(s => s.SensorSlug == slug);
            if (exists) return;

            context.SensorLocations.Add(new SensorLocation
            {
                SensorSlug = slug,
                Latitude = null,
                Longitude = null,
                Zone = ""
            });

            context.SaveChanges();
        }
    }
}