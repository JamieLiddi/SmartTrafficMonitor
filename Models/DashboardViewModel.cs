using System.Collections.Generic;

namespace SmartTrafficMonitor.Models
{
    public class DashboardViewModel
    {
        public TrafficFilterModel Filters { get; set; }

        // Results from the query (after applying filters and paging)
        public List<TrafficData> Results { get; set; }

        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
