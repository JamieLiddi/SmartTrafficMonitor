// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Microsoft.EntityFrameworkCore;
// using SmartTrafficMonitor.Models;

// namespace SmartTrafficMonitor.Services
// {
//     public static class SensorLocationService
//     {
//         // Default zone centres TEMPORARY UNTIL DATASET PROVIDED PROPERLY
//         private const double FootscrayLat = -37.7985;
//         private const double FootscrayLng = 144.9015;

//         private const double VuLat = -37.8070;
//         private const double VuLng = 144.8990;

//         public static Dictionary<int, SensorLocation> GetOrCreateLocations(
//             ApplicationDbContext context,
//             IEnumerable<int> sensorIds,
//             string zone
//         )
//         {
//             var ids = sensorIds.Distinct().ToList();
//             if (ids.Count == 0) return new Dictionary<int, SensorLocation>();

//             var existing = context.SensorLocations
//                 .Where(s => ids.Contains(s.SensorId))
//                 .ToList();

//             var existingMap = existing.ToDictionary(x => x.SensorId, x => x);

//             var missing = ids.Where(id => !existingMap.ContainsKey(id)).ToList();
//             if (missing.Count == 0) return existingMap;

//             // Create missing locations ONCE and persist them.
//             var useVu = !string.IsNullOrWhiteSpace(zone) && zone.Trim().ToLowerInvariant().Contains("vu");

//             var centerLat = useVu ? VuLat : FootscrayLat;
//             var centerLng = useVu ? VuLng : FootscrayLng;

//             foreach (var sensorId in missing)
//             {
//                 // Generate unique but consistent coordinates around the zone center.
//                 var a = (sensorId % 10) - 5;
//                 var b = ((sensorId / 10) % 10) - 5;

//                 var loc = new SensorLocation
//                 {
//                     SensorId = sensorId,
//                     Latitude = centerLat + (a * 0.0009),
//                     Longitude = centerLng + (b * 0.0011),
//                     Zone = useVu ? "VU Campus" : "Footscray Park"
//                 };

//                 context.SensorLocations.Add(loc);
//                 existingMap[sensorId] = loc;
//             }

//             context.SaveChanges();

//             return existingMap;
//         }
//     }
// }