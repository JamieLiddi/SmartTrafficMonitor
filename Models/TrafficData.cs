using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTrafficMonitor.Models
{
    
    [Table("traffictable", Schema = "public")]
    public class TrafficData
    {
        [Key]
        [Column("sensor_id")]
        public int SensorId { get; set; }

        // "timestamp" is a reserved word; mapping avoids quoting issues
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Column("movement_type")]
        public string MovementType { get; set; }

        [Column("season")]
        public string Season { get; set; }

        [Column("foot_traffic_count")]
        public int FootTrafficCount { get; set; }

        [Column("vehicle_count")]
        public int VehicleCount { get; set; }

        [Column("public_transport_ref")]
        public bool PublicTransportRef { get; set; }

        [Column("vu_schedule_ref")]
        public bool VuScheduleRef { get; set; }
    }

    // View/filter model (not an EF entity) – you can include extra fields freely here
    public class TrafficFilterModel
    {
        public string Zone { get; set; }
        public string HeatmapPeriod { get; set; } // e.g., Weekly, Monthly, Seasonal
        public string MovementType { get; set; }  // e.g., pedestrian, vehicle, cyclist
        public string Direction { get; set; }
        public string Season { get; set; }
        public int? FootTrafficCount { get; set; }
        public int? VehicleCount { get; set; }
        public bool? PublicTransportRef { get; set; }
        public bool? VUScheduleRef { get; set; }
        public DateTime? TimeStamp { get; set; }
        public DateTime? TimeStampStart { get; set; }
        public DateTime? TimeStampEnd { get; set; }
        public string ExportFormat { get; set; } // "csv" or "pdf"
    }
}

namespace SmartTrafficMonitor.Services
{
    using SmartTrafficMonitor.Models;
    using System.Text;

    public static class HeatmapService
    {
        public static string GenerateHeatmap(string zone, string period)
        {
            // Stub implementation
            return $"https://example.com/heatmap?zone={zone}&period={period}";
        }
    }

    public static class ExportService
    {
        public static byte[] GenerateCsv(List<TrafficData> data)
        {
            // CSV columns match the DB schema exactly
            var sb = new StringBuilder();
            sb.AppendLine("sensor_id,timestamp,movement_type,season,foot_traffic_count,vehicle_count,public_transport_ref,vu_schedule_ref");

            foreach (var r in data)
            {
                // Basic CSV escaping for strings (wrap in quotes, double quotes inside)
                string Esc(string s) => s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

                sb.Append(r.SensorId).Append(',');
                sb.Append(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
                sb.Append(Esc(r.MovementType)).Append(',');
                sb.Append(Esc(r.Season)).Append(',');
                sb.Append(r.FootTrafficCount).Append(',');
                sb.Append(r.VehicleCount).Append(',');
                sb.Append(r.PublicTransportRef ? "true" : "false").Append(',');
                sb.AppendLine(r.VuScheduleRef ? "true" : "false");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] GeneratePdf(List<TrafficData> data)
        {
            // Stub implementation
            return Array.Empty<byte>();
        }
    }

    public static class DataService
    {
        public static List<TrafficData> GetFilteredData(TrafficFilterModel filters)
        {
            // Stub implementation
            return new List<TrafficData>();
        }
    }
}
