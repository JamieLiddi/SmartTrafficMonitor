using System.Collections.Generic;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Models
{
    public class EventImpactViewModel
    {
        public EventImpactScenario Scenario { get; set; } = new();

        public List<string> AvailableZones { get; set; } = new();

        // If you've added the sensor dropdown
        public List<string> AvailableSensors { get; set; } = new();

        // ✅ FIX: use ProjectionPoint (what your service returns)
        public List<ProjectionPoint>? Results { get; set; }

        public double? TotalBaseline { get; set; }
        public double? TotalProjected { get; set; }
        public double? Delta { get; set; }
    }
}