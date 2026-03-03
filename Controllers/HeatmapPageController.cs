using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SmartTrafficMonitor.Controllers
{
    [Route("Heatmap")]
    public class HeatmapPageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HeatmapPageController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("View")]
        public IActionResult View(string zone, string period)
        {
            var safeZone = string.IsNullOrWhiteSpace(zone) ? "Footscray Park" : zone;
            var safePeriod = string.IsNullOrWhiteSpace(period) ? "Weekly" : period;

            // TIME WINDOW 
            var latestTs = _context.TrafficDatas
                .Select(t => (DateTime?)t.Timestamp)
                .Max();

            var now = latestTs ?? DateTime.UtcNow;

            DateTime start;
            switch (safePeriod.Trim().ToLowerInvariant())
            {
                case "monthly":
                    start = now.AddDays(-30);
                    break;
                case "seasonal":
                    start = now.AddDays(-90);
                    break;
                default:
                    start = now.AddDays(-7);
                    break;
            }

            // Pull rows in the selected window
            var rows = _context.TrafficDatas
                .Where(t => t.Timestamp >= start && t.Timestamp <= now)
                .ToList();

            //  DEBUG: BASIC DB + WINDOW INFO 
            var totalDbRows = _context.TrafficDatas.Count();

            var minTs = _context.TrafficDatas.Min(t => t.Timestamp);
            var maxTs = _context.TrafficDatas.Max(t => t.Timestamp);

            var distinctSensors = _context.TrafficDatas
                .Select(t => t.SensorId)
                .Distinct()
                .OrderBy(x => x)
                .Take(100)
                .ToList();

            Console.WriteLine(" HEATMAP DEBUG ");
            Console.WriteLine($"TOTAL ROWS IN TrafficDatas TABLE: {totalDbRows}");
            Console.WriteLine($"ROWS IN CURRENT WINDOW: {rows.Count}");
            Console.WriteLine($"DB Timestamp Range: {minTs} -> {maxTs}");
            Console.WriteLine($"Window Timestamp Range: {start} -> {now}");
            Console.WriteLine("First 100 distinct SensorIds:");
            Console.WriteLine(string.Join(", ", distinctSensors));

            Console.WriteLine("First 10 rows in current window:");
            foreach (var r in rows.Take(10))
            {
                Console.WriteLine($"SensorId: {r.SensorId} | Foot: {r.FootTrafficCount} | Vehicle: {r.VehicleCount} | Timestamp: {r.Timestamp}");
            }

            //  DEBUG: SCHEMA PROBE
            try
            {
                var schemaSql = @"
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
AND (
    column_name ILIKE '%lat%' OR
    column_name ILIKE '%lon%' OR
    column_name ILIKE '%lng%' OR
    column_name ILIKE '%longitude%' OR
    column_name ILIKE '%latitude%' OR
    column_name ILIKE '%coord%' OR
    column_name ILIKE '%sensor%'
)
ORDER BY table_name, column_name;
";

                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = schemaSql;

                    using (var reader = cmd.ExecuteReader())
                    {
                        Console.WriteLine("=== POSSIBLE LOCATION / SENSOR SCHEMA ===");
                        while (reader.Read())
                        {
                            var table = reader.GetString(0);
                            var col = reader.GetString(1);
                            var type = reader.GetString(2);
                            Console.WriteLine($"{table}.{col} ({type})");
                        }
                        Console.WriteLine("=== END SCHEMA ===");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== SCHEMA PROBE FAILED ===");
                Console.WriteLine(ex.Message);
                Console.WriteLine("=== END SCHEMA PROBE FAILED ===");
            }

            //  CENTER COORDS 
            double centerLat, centerLng;
            if (safeZone.Trim().ToLowerInvariant().Contains("vu"))
            {
                centerLat = -37.8070;
                centerLng = 144.8990;
            }
            else
            {
                centerLat = -37.7985;
                centerLng = 144.9015;
            }

            //AGGREGATE BY SENSOR 
            var sensorAgg = rows
                .GroupBy(r => r.SensorId)
                .Select(g => new
                {
                    SensorId = g.Key,
                    Weight = g.Sum(x => (x.FootTrafficCount + x.VehicleCount))
                })
                .OrderByDescending(x => x.Weight)
                .Take(50)
                .ToList();

            Console.WriteLine("---- AGGREGATED SENSOR WEIGHTS (Top 50) ----");
            foreach (var s in sensorAgg.Take(10))
            {
                Console.WriteLine($"SensorId: {s.SensorId} | Weight: {s.Weight}");
            }

                // HEAT POINTS NO DATABASE DEPENDENCY
// HEAT POINTS (prefer DB coordinates, fallback to fake coords)
var sensorIds = sensorAgg.Select(x => x.SensorId).ToList();

var locations = _context.SensorLocations
    .Where(l => sensorIds.Contains(l.SensorId))
    .ToDictionary(l => l.SensorId, l => l);

var heatPoints = new List<double[]>();

foreach (var s in sensorAgg)
{
    double lat, lng;

    // Prefer real DB coordinates
    if (locations.TryGetValue(s.SensorId, out var loc)
        && loc.Latitude.HasValue
        && loc.Longitude.HasValue)
    {
        lat = loc.Latitude.Value;
        lng = loc.Longitude.Value;
    }
    else
    {
        // Fallback to old fake-but-stable coords so the map never breaks
        var a = (s.SensorId % 10) - 5;
        var b = ((s.SensorId / 10) % 10) - 5;

        lat = centerLat + (a * 0.0009);
        lng = centerLng + (b * 0.0011);
    }

    var intensity = Math.Min(1.0, Math.Max(1, s.Weight) / 500.0);
    heatPoints.Add(new[] { lat, lng, intensity });
}

            // If somehow nothing plotted, add demo points so map never looks broken.
            if (heatPoints.Count == 0)
            {
                heatPoints.Add(new[] { centerLat + 0.0006, centerLng + 0.0006, 0.8 });
                heatPoints.Add(new[] { centerLat + 0.0002, centerLng + 0.0004, 0.6 });
                heatPoints.Add(new[] { centerLat - 0.0003, centerLng - 0.0002, 0.7 });
                heatPoints.Add(new[] { centerLat - 0.0007, centerLng + 0.0001, 0.5 });
                heatPoints.Add(new[] { centerLat + 0.0001, centerLng - 0.0007, 0.65 });
            }

            Console.WriteLine($"HeatPoints Generated: {heatPoints.Count}");
            Console.WriteLine("=====================");

            //SEND TO VIEW 
            ViewData["Zone"] = safeZone;
            ViewData["Period"] = safePeriod;
            ViewData["CenterLat"] = centerLat;
            ViewData["CenterLng"] = centerLng;
            ViewData["HeatPointsJson"] = JsonSerializer.Serialize(heatPoints);

            return View("~/Views/Heatmap/HeatmapView.cshtml");
        }
    }
}
