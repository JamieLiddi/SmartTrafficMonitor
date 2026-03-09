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
            var safeZone = string.IsNullOrWhiteSpace(zone) ? "All" : zone.Trim();
            var safePeriod = string.IsNullOrWhiteSpace(period) ? "Weekly" : period.Trim();

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

            double centerLat, centerLng;
            if (safeZone.Equals("VU Campus", StringComparison.OrdinalIgnoreCase))
            {
                centerLat = -37.7948;
                centerLng = 144.9033;
            }
            else
            {
                centerLat = -37.7985;
                centerLng = 144.9015;
            }

            var allLocations = _context.SensorLocations
                .AsNoTracking()
                .ToList();

            var vuCampusSlugSet = new HashSet<string>(
                new[]
                {
                    "footscray-park-gardens",
                    "footscray-park-rowing-club",
                    "salt-water-child-care-centre"
                }.Select(NormSlug)
            );

            List<string> zoneSensorSlugs;

            if (safeZone.Equals("VU Campus", StringComparison.OrdinalIgnoreCase))
            {
                zoneSensorSlugs = allLocations
                    .Where(l => vuCampusSlugSet.Contains(NormSlug(l.SensorSlug)))
                    .Select(l => l.SensorSlug)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            else if (safeZone.Equals("Footscray City", StringComparison.OrdinalIgnoreCase))
            {
                zoneSensorSlugs = allLocations
                    .Where(l => !vuCampusSlugSet.Contains(NormSlug(l.SensorSlug)))
                    .Select(l => l.SensorSlug)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            else // All
            {
                zoneSensorSlugs = allLocations
                    .Select(l => l.SensorSlug)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            var zoneSensorSlugSet = zoneSensorSlugs
                .Select(NormSlug)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var windowQuery = _context.TrafficDatas
                .AsNoTracking()
                .Where(t => t.Timestamp >= start && t.Timestamp <= now);

            if (zoneSensorSlugSet.Count > 0)
            {
                var zoneRawSlugs = zoneSensorSlugs
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList();

                windowQuery = windowQuery.Where(t => zoneRawSlugs.Contains(t.SensorId));
            }

            var sensorAgg = windowQuery
                .GroupBy(r => r.SensorId)
                .Select(g => new
                {
                    SensorId = g.Key,
                    Pedestrians = g.Sum(x => x.MovementType == "Pedestrian" ? x.FootTrafficCount : 0),
                    Cyclists = g.Sum(x => x.MovementType == "Cyclist" ? x.FootTrafficCount : 0),
                    Vehicles = g.Sum(x => x.VehicleCount),
                    Weight = g.Sum(x => (x.FootTrafficCount + x.VehicleCount))
                })
                .Select(x => new
                {
                    x.SensorId,
                    x.Pedestrians,
                    x.Cyclists,
                    x.Vehicles,
                    x.Weight,
                    Total = x.Pedestrians + x.Cyclists + x.Vehicles
                })
                .OrderByDescending(x => x.Total)
                .Take(50)
                .ToList();

            var sensorSlugs = sensorAgg.Select(x => x.SensorId).ToList();
            var sensorSlugNormSet = sensorSlugs.Select(NormSlug).ToHashSet();

            var locationsNorm = allLocations
                .Where(l => sensorSlugNormSet.Contains(NormSlug(l.SensorSlug)))
                .ToDictionary(l => NormSlug(l.SensorSlug), l => l);

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

                markers.Add(new
                {
                    slug = s.SensorId ?? "",
                    lat,
                    lng,
                    pedestrians = s.Pedestrians,
                    cyclists = s.Cyclists,
                    vehicles = s.Vehicles,
                    total = s.Total,
                    weight = s.Weight
                });
            }
            // If no data points, add some demo points around the center
            if (heatPoints.Count == 0)
            {
                heatPoints.Add(new[] { centerLat + 0.0006, centerLng + 0.0006, 0.8 });
                heatPoints.Add(new[] { centerLat + 0.0002, centerLng + 0.0004, 0.6 });
                heatPoints.Add(new[] { centerLat - 0.0003, centerLng - 0.0002, 0.7 });
                heatPoints.Add(new[] { centerLat - 0.0007, centerLng + 0.0001, 0.5 });
                heatPoints.Add(new[] { centerLat + 0.0001, centerLng - 0.0007, 0.65 });

                markers.Clear();
                markers.Add(new { slug = "demo-a", lat = centerLat + 0.0006, lng = centerLng + 0.0006, pedestrians = 500, cyclists = 60, vehicles = 240, total = 800, weight = 800 });
                markers.Add(new { slug = "demo-b", lat = centerLat + 0.0002, lng = centerLng + 0.0004, pedestrians = 380, cyclists = 40, vehicles = 180, total = 600, weight = 600 });
                markers.Add(new { slug = "demo-c", lat = centerLat - 0.0003, lng = centerLng - 0.0002, pedestrians = 420, cyclists = 55, vehicles = 225, total = 700, weight = 700 });
                markers.Add(new { slug = "demo-d", lat = centerLat - 0.0007, lng = centerLng + 0.0001, pedestrians = 310, cyclists = 30, vehicles = 160, total = 500, weight = 500 });
                markers.Add(new { slug = "demo-e", lat = centerLat + 0.0001, lng = centerLng - 0.0007, pedestrians = 360, cyclists = 45, vehicles = 245, total = 650, weight = 650 });

                usedRealCoords = 0;
                usedFallbackCoords = markers.Count;
            }

            int low = 0, mid = 0, high = 0;
            foreach (var p in heatPoints)
            {
                var intensity = p.Length >= 3 ? p[2] : 0.0;
                if (intensity < 0.35) low++;
                else if (intensity < 0.70) mid++;
                else high++;
            }

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

            ViewData["RealCoordsCount"] = usedRealCoords;
            ViewData["FallbackCoordsCount"] = usedFallbackCoords;
            ViewData["ZoneSensorsCount"] = zoneSensorSlugSet.Count;

            return View("~/Views/Heatmap/HeatmapView.cshtml");
        }
    }
}