using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public class ProjectionPoint
    {
        public int Hour { get; set; }
        public double Baseline { get; set; }
        public double Projected { get; set; }
        public double Lower { get; set; }
        public double Upper { get; set; }
        public bool IsImpacted { get; set; }
    }

    public interface IEventImpactProjectionService
    {
        List<ProjectionPoint> ProjectHourly(EventImpactScenario scenario);
    }

    public class EventImpactProjectionService : IEventImpactProjectionService
    {
        private readonly ApplicationDbContext _context;

        public EventImpactProjectionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ProjectionPoint> ProjectHourly(EventImpactScenario s)
        {
            // Treat the chosen scenario date as a LOCAL day (user is picking a local date)
            // Then query the DB using UTC boundaries.
            var localDayStart = DateTime.SpecifyKind(s.Date.Date, DateTimeKind.Local);
            var localDayEndExclusive = localDayStart.AddDays(1);

            // Lookback window (in local days, but converted to UTC for DB query)
            var lookbackLocalStart = localDayStart.AddDays(-(s.LookbackWeeks * 7));

            var startUtc = lookbackLocalStart.ToUniversalTime();
            var endUtc = localDayEndExclusive.ToUniversalTime().AddTicks(-1);

            var targetDowLocal = s.OverrideDayOfWeek ?? localDayStart.DayOfWeek;

            // Base traffic window in UTC
            var traffic = _context.TrafficDatas.AsNoTracking()
                .Where(t => t.Timestamp >= startUtc && t.Timestamp <= endUtc);

            // Join for zone filtering (zone may be incomplete, but keep existing behaviour)
            var joined = from t in traffic
                         join sl in _context.SensorLocations.AsNoTracking()
                             on t.SensorId equals sl.SensorSlug into slj
                         from sl in slj.DefaultIfEmpty()
                         select new
                         {
                             t.Timestamp,          // timestamp with tz -> comes through as UTC DateTime
                             t.MovementType,
                             t.FootTrafficCount,
                             t.VehicleCount,
                             Zone = sl != null ? sl.Zone : null,
                             SensorId = t.SensorId
                         };

            // If a specific sensor is selected, filter by it (this should override zone in your UI)
            if (!string.IsNullOrWhiteSpace(s.SensorId) && s.SensorId != "All")
            {
                joined = joined.Where(x => x.SensorId == s.SensorId);
            }
            else if (!string.IsNullOrWhiteSpace(s.Zone) && s.Zone != "All")
            {
                joined = joined.Where(x => x.Zone == s.Zone);
            }

            // Materialize then convert to LOCAL for DOW + Hour grouping
            var rowsLocal = joined
                .ToList()
                .Select(x => new
                {
                    LocalTime = x.Timestamp.ToLocalTime(),
                    x.MovementType,
                    x.FootTrafficCount,
                    x.VehicleCount
                })
                .Where(x => x.LocalTime.DayOfWeek == targetDowLocal)
                .ToList();

            bool MovementMatches(string movementType)
            {
                return s.Movement switch
                {
                    ProjectionMovement.Pedestrian => movementType == "Pedestrian",
                    ProjectionMovement.Cyclist => movementType == "Cyclist",
                    ProjectionMovement.PedestrianPlusCyclist => movementType == "Pedestrian" || movementType == "Cyclist",
                    ProjectionMovement.Vehicle => movementType == "Vehicle",
                    _ => false
                };
            }

            double GetValue(string movementType, int? footTraffic, int? vehicle)
            {
                // Vehicles use VehicleCount, others use FootTrafficCount
                if (s.Movement == ProjectionMovement.Vehicle)
                    return vehicle ?? 0;

                return footTraffic ?? 0;
            }

            var filtered = rowsLocal
                .Where(r => !string.IsNullOrWhiteSpace(r.MovementType) && MovementMatches(r.MovementType))
                .ToList();

            // Baseline per LOCAL hour (0-23)
            var baselineByHour = filtered
                .GroupBy(r => r.LocalTime.Hour)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(r => GetValue(r.MovementType!, r.FootTrafficCount, r.VehicleCount))
                );

            bool IsInWindow(int hour)
            {
                var endHour = s.StartHour + s.DurationHours;
                if (endHour <= 24) return hour >= s.StartHour && hour < endHour;

                // wraps past midnight
                var wrapEnd = endHour % 24;
                return (hour >= s.StartHour && hour < 24) || (hour >= 0 && hour < wrapEnd);
            }

            var eventMult = (s.HasEvent ? (1.0 + (s.EventUpliftPercent / 100.0)) : 1.0);
            var vuMult = (s.HasVuImpact ? (1.0 + (s.VuUpliftPercent / 100.0)) : 1.0);
            var band = Math.Max(0, s.UncertaintyPercent) / 100.0;

            var points = new List<ProjectionPoint>(24);

            for (int hour = 0; hour < 24; hour++)
            {
                var baseline = baselineByHour.TryGetValue(hour, out var b) ? b : 0.0;
                var impacted = IsInWindow(hour);

                var mult = impacted ? (eventMult * vuMult) : 1.0;
                var projected = baseline * mult;

                points.Add(new ProjectionPoint
                {
                    Hour = hour,
                    Baseline = Math.Round(baseline, 2),
                    Projected = Math.Round(projected, 2),
                    Lower = Math.Round(projected * (1.0 - band), 2),
                    Upper = Math.Round(projected * (1.0 + band), 2),
                    IsImpacted = impacted
                });
            }

            return points;
        }
    }
}