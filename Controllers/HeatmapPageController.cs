using Microsoft.AspNetCore.Mvc;
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

            // Choose a time window
            var now = DateTime.UtcNow;
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

            // (These are approximate Footscray/VU coords; enough to display a working heatmap layer)
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

            // Pull recent data 
            var rows = _context.TrafficDatas
                .Where(t => t.Timestamp >= start && t.Timestamp <= now)
                .ToList();

            // Aggregate by SensorId to create “hot spots”
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

            // This guarantees the heatmap ALWAYS appears.
            var heatPoints = new List<double[]>();

            foreach (var s in sensorAgg)
            {
                
                var a = (s.SensorId % 10) - 5;          
                var b = ((s.SensorId / 10) % 10) - 5;    

                var lat = centerLat + (a * 0.0009);
                var lng = centerLng + (b * 0.0011);


                var w = Math.Max(1, s.Weight);
                var intensity = Math.Min(1.0, w / 500.0); 
                heatPoints.Add(new[] { lat, lng, intensity });
            }
        
            ViewData["Zone"] = safeZone;
            ViewData["Period"] = safePeriod;
            ViewData["CenterLat"] = centerLat;
            ViewData["CenterLng"] = centerLng;
            ViewData["HeatPointsJson"] = JsonSerializer.Serialize(heatPoints);

            
            return View("~/Views/Heatmap/HeatmapView.cshtml");
        }
    }
}
