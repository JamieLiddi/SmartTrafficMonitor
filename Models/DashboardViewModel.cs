using System.Collections.Generic;

namespace SmartTrafficMonitor.Models
{
    public class DashboardViewModel
    {
        public TrafficFilterModel Filters { get; set; } = new();

        // ✅ ADD THIS
        public List<string> AvailableSensors { get; set; } = new();

        // existing stuff...
        public List<TrafficData> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }

        public bool ShowFallbackWarning { get; set; }
        public string? FallbackMessage { get; set; }
    }
}