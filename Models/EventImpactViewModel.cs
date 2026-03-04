using System.Collections.Generic;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Models
{
    public class EventImpactViewModel
    {
        public EventImpactScenario Scenario { get; set; } = new();
        public List<string> AvailableZones { get; set; } = new();
        public List<ProjectionPoint>? Results { get; set; }

        public double? TotalBaseline { get; set; }
        public double? TotalProjected { get; set; }
        public double? Delta { get; set; }
    }
}