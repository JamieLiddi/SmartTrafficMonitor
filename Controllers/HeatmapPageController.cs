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

        [HttpGet("View")]
        public IActionResult View(string zone, string period)
        {
            var safeZone = string.IsNullOrWhiteSpace(zone) ? "Footscray Park" : zone;
            var safePeriod = string.IsNullOrWhiteSpace(period) ? "Weekly" : period;

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

            // Pull rows in the selected window (queryable)
            var windowQuery = _context.TrafficDatas
                .AsNoTracking()
                .Where(t => t.Timestamp >= start && t.Timestamp <= now);

            // AGGREGATE BY SENSOR (top 50)
            var sensorAgg = windowQuery
                .GroupBy(r => r.SensorId)
                .Select(g => new
                {
                    SensorId = g.Key,
                    Weight = g.Sum(x => (x.FootTrafficCount + x.VehicleCount))
                })
                .OrderByDescending(x => x.Weight)
                .Take(50)
                .ToList();

            // Pull locations for only the sensors we’re plotting
            var sensorSlugs = sensorAgg.Select(x => x.SensorId).ToList();

            var locations = _context.SensorLocations
                .AsNoTracking()
                .Where(l => sensorSlugs.Contains(l.SensorSlug))
                .ToDictionary(l => l.SensorSlug, l => l);

            // Dynamic scaling: intensity relative to maxWeight
            var maxWeight = sensorAgg.Count > 0 ? sensorAgg.Max(x => x.Weight) : 0;

            var heatPoints = new List<double[]>();

            // ✅ NEW: marker payload (same coordinates as heat points)
            var markers = new List<object>();

            foreach (var s in sensorAgg)
            {
                double lat, lng;

                if (!string.IsNullOrWhiteSpace(s.SensorId)
                    && locations.TryGetValue(s.SensorId, out var loc)
                    && loc.Latitude.HasValue
                    && loc.Longitude.HasValue)
                {
                    lat = loc.Latitude.Value;
                    lng = loc.Longitude.Value;
                }
                else
                {
                    // Stable fallback so map never breaks
                    var h = Math.Abs((s.SensorId ?? "").GetHashCode());
                    var a = (h % 10) - 5;
                    var b = ((h / 10) % 10) - 5;

                    lat = centerLat + (a * 0.0009);
                    lng = centerLng + (b * 0.0011);
                }

                var intensity = maxWeight <= 0 ? 0.0 : (double)s.Weight / maxWeight;
                intensity = Math.Clamp(intensity, 0.05, 1.0);

                heatPoints.Add(new[] { lat, lng, intensity });

                // ✅ NEW: markers (slug + coords + weight)
                markers.Add(new
                {
                    slug = s.SensorId ?? "",
                    lat,
                    lng,
                    weight = s.Weight
                });
            }

            // If empty, add demo points so map never looks broken.
            if (heatPoints.Count == 0)
            {
                heatPoints.Add(new[] { centerLat + 0.0006, centerLng + 0.0006, 0.8 });
                heatPoints.Add(new[] { centerLat + 0.0002, centerLng + 0.0004, 0.6 });
                heatPoints.Add(new[] { centerLat - 0.0003, centerLng - 0.0002, 0.7 });
                heatPoints.Add(new[] { centerLat - 0.0007, centerLng + 0.0001, 0.5 });
                heatPoints.Add(new[] { centerLat + 0.0001, centerLng - 0.0007, 0.65 });

                // ✅ NEW: demo markers matching demo points
                markers.Clear();
                markers.Add(new { slug = "demo-a", lat = centerLat + 0.0006, lng = centerLng + 0.0006, weight = 800 });
                markers.Add(new { slug = "demo-b", lat = centerLat + 0.0002, lng = centerLng + 0.0004, weight = 600 });
                markers.Add(new { slug = "demo-c", lat = centerLat - 0.0003, lng = centerLng - 0.0002, weight = 700 });
                markers.Add(new { slug = "demo-d", lat = centerLat - 0.0007, lng = centerLng + 0.0001, weight = 500 });
                markers.Add(new { slug = "demo-e", lat = centerLat + 0.0001, lng = centerLng - 0.0007, weight = 650 });
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

            // ✅ NEW: marker json
            ViewData["MarkersJson"] = JsonSerializer.Serialize(markers);

            // HUD counts
            ViewData["PointsCount"] = heatPoints.Count;
            ViewData["LowCount"] = low;
            ViewData["MidCount"] = mid;
            ViewData["HighCount"] = high;

            return View("~/Views/Heatmap/HeatmapView.cshtml");
        }
    }
}