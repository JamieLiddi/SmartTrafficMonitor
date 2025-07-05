using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartTrafficMonitor.Models
{
    public class TrafficData
    {
        //Creating Traffic Data values
        public int Id { get; set; }
        public string SensorId { get; set; }
        public DateTime TimeStamp { get; set; }
        public string MovementType { get; set; } //e.g pedestrian, vehicle, cyclist
        public string Direction { get; set; }
        public string Season { get; set; }
        public int FootTrafficCount { get; set; }
        public int VehicleCount { get; set; }
        public bool PublicTransportRef { get; set; }
        public bool VUScheduleRef {  get; set; }
        public string HeatmapPeriod { get; set; } //e.g. Weekly, Monthly, Seasonal
        //Any more needed
    }
}
