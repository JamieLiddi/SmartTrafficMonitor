// Controllers/HeatmapPageController.cs
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

        private static string NormSlug(string? s)
        {
            return (s ?? "")
                .Trim()
                .Replace("_", "-")
                .ToLowerInvariant();
        }

        [HttpGet("View")]
        public IActionResult View(string zone, string period)
        {
            var safeZone = string.IsNullOrWhiteSpace(zone) ? "Footscray Park" : zone.Trim();
            var safePeriod = string.IsNullOrWhiteSpace(period) ? "Weekly" : period.Trim();

            // TIME WINDOW (anchor to latest DB timestamp so window is never empty)
            var latestTs = _context.TrafficDatas
                .AsNoTracking()
                .Select(t => (DateTime?)t.Timestamp)
                .Max();

            var now = latestTs ?? DateTime.UtcNow;

            DateTime start = safePeriod.Trim().ToLowerInvariant() switch
            {
                "monthly" => now.AddDays(-30),
                "seasonal" => now.AddDays(-90),
                _ => now.AddDays(-7),
            };

            // CENTER COORDS (map view)
            double centerLat, centerLng;
            if (safeZone.ToLowerInvariant().Contains("vu"))
            {
                centerLat = -37.8070;
                centerLng = 144.8990;
            }
            else
            {
                centerLat = -37.7985;
                centerLng = 144.9015;
            }

            // Zone-based sensor filter (so VU doesn't show Footscray sensors)
            var zoneSensorSlugs = _context.SensorLocations
                .AsNoTracking()
                .Where(l => l.Zone == safeZone)
                .Select(l => l.SensorSlug)
                .ToList();

            var zoneSensorSlugSet = zoneSensorSlugs
                .Select(NormSlug)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            // Pull rows in the selected window (queryable)
            var windowQuery = _context.TrafficDatas
                .AsNoTracking()
                .Where(t => t.Timestamp >= start && t.Timestamp <= now);

            // If we have zone sensors, filter the window query to that zone’s sensors only
            if (zoneSensorSlugSet.Count > 0)
            {
                var zoneRawSlugs = zoneSensorSlugs
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList();

                windowQuery = windowQuery.Where(t => zoneRawSlugs.Contains(t.SensorId));
            }

            // ✅ AGGREGATE BY SENSOR (top 50) WITH SPLITS
            var sensorAgg = windowQuery
                .GroupBy(r => r.SensorId)
                .Select(g => new
                {
                    SensorId = g.Key,
                    Pedestrians = g.Sum(x => x.FootTrafficCount),
                    Cyclists = g.Sum(x => x.CyclistCount),
                    Vehicles = g.Sum(x => x.VehicleCount)
                })
                .Select(x => new
                {
                    x.SensorId,
                    x.Pedestrians,
                    x.Cyclists,
                    x.Vehicles,
                    Total = x.Pedestrians + x.Cyclists + x.Vehicles
                })
                .OrderByDescending(x => x.Total)
                .Take(50)
                .ToList();

            // Pull locations for only the sensors we’re plotting (normalize keying)
            var sensorSlugs = sensorAgg.Select(x => x.SensorId).ToList();
            var sensorSlugNormSet = sensorSlugs.Select(NormSlug).ToHashSet();

            // Note: Keeping your in-memory normalize filter (works fine for 11 rows)
            var locationsNorm = _context.SensorLocations
                .AsNoTracking()
                .ToList()
                .Where(l => sensorSlugNormSet.Contains(NormSlug(l.SensorSlug)))
                .ToDictionary(l => NormSlug(l.SensorSlug), l => l);

            // ✅ Dynamic scaling: intensity relative to maxTotal
            var maxTotal = sensorAgg.Count > 0 ? sensorAgg.Max(x => x.Total) : 0;

            var heatPoints = new List<double[]>();
            var markers = new List<object>();

            int usedRealCoords = 0;
            int usedFallbackCoords = 0;

            foreach (var s in sensorAgg)
            {
                double lat, lng;

                var key = NormSlug(s.SensorId);

                if (!string.IsNullOrWhiteSpace(key)
                    && locationsNorm.TryGetValue(key, out var loc)
                    && loc.Latitude.HasValue
                    && loc.Longitude.HasValue)
                {
                    lat = loc.Latitude.Value;
                    lng = loc.Longitude.Value;
                    usedRealCoords++;
                }
                else
                {
                    // Stable fallback so map never breaks
                    var h = Math.Abs((s.SensorId ?? "").GetHashCode());
                    var a = (h % 10) - 5;
                    var b = ((h / 10) % 10) - 5;

                    lat = centerLat + (a * 0.0009);
                    lng = centerLng + (b * 0.0011);
                    usedFallbackCoords++;
                }

                var intensity = maxTotal <= 0 ? 0.0 : (double)s.Total / maxTotal;
                intensity = Math.Clamp(intensity, 0.05, 1.0);

                heatPoints.Add(new[] { lat, lng, intensity });

                // ✅ Markers now include split totals for tooltip
                markers.Add(new
                {
                    slug = s.SensorId ?? "",
                    lat,
                    lng,
                    pedestrians = s.Pedestrians,
                    cyclists = s.Cyclists,
                    vehicles = s.Vehicles,
                    total = s.Total
                });
            }

            if (heatPoints.Count == 0)
            {
                heatPoints.Add(new[] { centerLat + 0.0006, centerLng + 0.0006, 0.8 });
                heatPoints.Add(new[] { centerLat + 0.0002, centerLng + 0.0004, 0.6 });
                heatPoints.Add(new[] { centerLat - 0.0003, centerLng - 0.0002, 0.7 });
                heatPoints.Add(new[] { centerLat - 0.0007, centerLng + 0.0001, 0.5 });
                heatPoints.Add(new[] { centerLat + 0.0001, centerLng - 0.0007, 0.65 });

                markers.Clear();
                markers.Add(new { slug = "demo-a", lat = centerLat + 0.0006, lng = centerLng + 0.0006, pedestrians = 300, cyclists = 50, vehicles = 450, total = 800 });
                markers.Add(new { slug = "demo-b", lat = centerLat + 0.0002, lng = centerLng + 0.0004, pedestrians = 250, cyclists = 30, vehicles = 320, total = 600 });
                markers.Add(new { slug = "demo-c", lat = centerLat - 0.0003, lng = centerLng - 0.0002, pedestrians = 280, cyclists = 40, vehicles = 380, total = 700 });
                markers.Add(new { slug = "demo-d", lat = centerLat - 0.0007, lng = centerLng + 0.0001, pedestrians = 200, cyclists = 25, vehicles = 275, total = 500 });
                markers.Add(new { slug = "demo-e", lat = centerLat + 0.0001, lng = centerLng - 0.0007, pedestrians = 240, cyclists = 35, vehicles = 375, total = 650 });

                usedRealCoords = 0;
                usedFallbackCoords = markers.Count;
            }

            // classify intensities into low/mid/high for HUD
            int low = 0, mid = 0, high = 0;
            foreach (var p in heatPoints)
            {
                var intensity = p.Length >= 3 ? p[2] : 0.0;
                if (intensity < 0.35) low++;
                else if (intensity < 0.70) mid++;
                else high++;
            }

            // SEND TO VIEW
            ViewData["Zone"] = safeZone;
            ViewData["Period"] = safePeriod;
            ViewData["CenterLat"] = centerLat;
            ViewData["CenterLng"] = centerLng;
            ViewData["HeatPointsJson"] = JsonSerializer.Serialize(heatPoints);
            ViewData["MarkersJson"] = JsonSerializer.Serialize(markers);

            ViewData["PointsCount"] = heatPoints.Count;
            ViewData["LowCount"] = low;
            ViewData["MidCount"] = mid;
            ViewData["HighCount"] = high;

            // Debug numbers
            ViewData["RealCoordsCount"] = usedRealCoords;
            ViewData["FallbackCoordsCount"] = usedFallbackCoords;
            ViewData["ZoneSensorsCount"] = zoneSensorSlugSet.Count;

            return View("~/Views/Heatmap/HeatmapView.cshtml");
        }
    }
}