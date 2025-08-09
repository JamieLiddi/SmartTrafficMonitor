using System.Collections.Generic;

namespace SmartTrafficMonitor.Models
{
    public class DashboardViewModel
    {
        public TrafficFilterModel Filters { get; set; }
        public List<TrafficData> Results { get; set; }
    }
}