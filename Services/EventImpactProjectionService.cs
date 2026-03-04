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
            var targetDow = s.OverrideDayOfWeek ?? s.Date.DayOfWeek;

            // Lookback window
            var end = DateTime.SpecifyKind(s.Date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            var start = end.AddDays(-(s.LookbackWeeks * 7));

            // Build query, then join to SensorLocations to filter zone.
            var traffic = _context.TrafficDatas.AsNoTracking()
                .Where(t => t.Timestamp >= start && t.Timestamp <= end);

            // Filter to target day-of-week (EF may not translate DayOfWeek reliably depending on provider)
            // We'll do this safely by pulling minimal columns then filtering in-memory after zone join.
            // For performance, keep the date window tight (LookbackWeeks) and zone filter early.

            // Join to sensor_locations
            var joined = from t in traffic
                         join sl in _context.SensorLocations.AsNoTracking()
                             on t.SensorId equals sl.SensorSlug into slj
                         from sl in slj.DefaultIfEmpty()
                         select new
                         {
                             t.Timestamp,
                             t.MovementType,
                             t.FootTrafficCount,
                             t.VehicleCount,
                             Zone = sl != null ? sl.Zone : null
                         };

            if (!string.IsNullOrWhiteSpace(s.Zone) && s.Zone != "All")
                joined = joined.Where(x => x.Zone == s.Zone);

            // Materialize
            var rows = joined.ToList()
                .Where(x => x.Timestamp.DayOfWeek == targetDow)
                .ToList();

            // Build baseline per hour (0-23)
            double GetValue(dynamic r)
            {
                // MovementType stored as string in your TrafficData model
                // Foot traffic for Ped/Cyc, VehicleCount for Vehicle
                return (double)(
                    (s.Movement == ProjectionMovement.Vehicle)
                        ? (r.VehicleCount ?? 0)
                        : (r.FootTrafficCount ?? 0)
                );
            }

            bool MovementMatches(dynamic r)
            {
                var mt = (string)r.MovementType;
                return s.Movement switch
                {
                    ProjectionMovement.Pedestrian => mt == "Pedestrian",
                    ProjectionMovement.Cyclist => mt == "Cyclist",
                    ProjectionMovement.PedestrianPlusCyclist => mt == "Pedestrian" || mt == "Cyclist",
                    ProjectionMovement.Vehicle => mt == "Vehicle",
                    _ => false
                };
            }

            var filtered = rows.Where(MovementMatches).ToList();

            var baselineByHour = filtered
                .GroupBy(r => r.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Average(r => GetValue(r)));

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