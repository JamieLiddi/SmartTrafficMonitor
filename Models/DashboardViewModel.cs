using System.Collections.Generic;

namespace SmartTrafficMonitor.Models
{
    public class DashboardViewModel
    {
        public TrafficFilterModel Filters { get; set; } = new();

        public List<string> AvailableSensors { get; set; } = new();

        // ✅ KPI fields
        public long KpiTotalFootTraffic { get; set; }     // Pedestrians
        public long KpiTotalCyclists { get; set; }        // NEW
        public long KpiTotalVehicles { get; set; }
        public long KpiRecordCount { get; set; }
        public string KpiBusiestSensor { get; set; } = "—";
        public string KpiPeakHour { get; set; } = "—";

        public List<TrafficData> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }

        public bool ShowFallbackWarning { get; set; }
        public string? FallbackMessage { get; set; }
    }
}