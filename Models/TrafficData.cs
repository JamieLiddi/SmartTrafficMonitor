using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;                 
using Microsoft.EntityFrameworkCore;

namespace SmartTrafficMonitor.Models
{
    [Table("traffictable", Schema = "public")]
    public class TrafficData
    {
        //Primary Key Row
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Sensor Location Identifier  
        [Column("SensorId")]
        public int SensorId { get; set; }

        [Column("TimeStamp")]
        public DateTime Timestamp { get; set; }

        [Column("MovementType")]
        public string? MovementType { get; set; }

        // Direction of movement 
        [Column("Direction")]
        public string? Direction { get; set; }

        [Column("Season")]
        public string? Season { get; set; }

        [Column("FootTrafficCount")]
        public int FootTrafficCount { get; set; }

        [Column("VehicleCount")]
        public int VehicleCount { get; set; }

        [Column("PublicTransportRef")]
        public bool PublicTransportRef { get; set; }

        [Column("VUScheduleRef")]
        public bool VuScheduleRef { get; set; }
    }

    // View/filter model (NOT an EF entity)
    public class TrafficFilterModel
    {
        // From your UI
        public int? SensorId { get; set; }
        public DateTime? From { get; set; }          // matches Index.cshtml
        public DateTime? To { get; set; }            // matches Index.cshtml

        public string MovementType { get; set; }     // Pedestrian/Vehicle/Cyclist/empty
        public string Direction { get; set; }        //Db column & UI filter
        public string Season { get; set; }           // Summer/Autumn/Winter/Spring/empty
        public bool? PublicTransportRef { get; set; }
        public bool? VUScheduleRef { get; set; }     
        [NotMapped] //Not curretnly a database column
        public bool? VuScheduleRef{
            get {return VUScheduleRef;}
            set { VUScheduleRef = value; }
        }

        public int? FootTrafficCount { get; set; }
        public int? VehicleCount { get; set; }

        // Heatmap inputs
        public string Zone { get; set; }
        public string HeatmapPeriod { get; set; }    // Weekly/Monthly/Seasonal

        // Legacy names (keep for compatibility if someone calls API with these)
        public DateTime? TimeStamp { get; set; }
        public DateTime? TimeStampStart { get; set; }
        public DateTime? TimeStampEnd { get; set; }

        // Export
        public string ExportFormat { get; set; }     // "csv" | "pdf"
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
            


            
            // StiLL TO DO!!!
            



            return $"/Heatmap/View?zone={Uri.EscapeDataString(zone ?? "")}&period={Uri.EscapeDataString(period ?? "")}";
        }
    }

    public static class ExportService
    {
        public static byte[] GenerateCsv(List<TrafficData> data)
        {
            // CSV columns match the DB schema exactly
            var sb = new StringBuilder();
            sb.AppendLine("sensor_id,timestamp,movement_type,direction,season,foot_traffic_count,vehicle_count,public_transport_ref,vu_schedule_ref");

            foreach (var r in data)
            {
                string Esc(string s) => s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

                sb.Append(r.SensorId).Append(',');
                sb.Append(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
                sb.Append(Esc(r.MovementType)).Append(',');
                sb.Append(Esc(r.Direction)).Append(',');
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
        public static List<TrafficData> GetFilteredData(ApplicationDbContext context, TrafficFilterModel filters)
        {
            var q = context.TrafficDatas.AsQueryable();

            if (filters == null)
                return q.OrderByDescending(t => t.Timestamp).ToList();


            var from = filters.From ?? filters.TimeStampStart ?? filters.TimeStamp;
            var to   = filters.To   ?? filters.TimeStampEnd   ?? filters.TimeStamp;

            if (from.HasValue)
                from = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            if (to.HasValue)
                to = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);


            if (filters.SensorId.HasValue)
                q = q.Where(t => t.SensorId == filters.SensorId.Value);

            if (!string.IsNullOrWhiteSpace(filters.MovementType))
                q = q.Where(t => t.MovementType == filters.MovementType);

            if (!string.IsNullOrWhiteSpace(filters.Direction))
            { q = q.Where(t => t.Direction == filters.Direction);
}
            //Filter by Season
            if (!string.IsNullOrWhiteSpace(filters.Season))
                q = q.Where(t => t.Season == filters.Season);

            //Filter by Transport and Schedule references
            if (filters.PublicTransportRef.HasValue)
                q = q.Where(t => t.PublicTransportRef == filters.PublicTransportRef.Value);

            //Filter by VU Schedule reference
            if (filters.VUScheduleRef.HasValue)
                q = q.Where(t => t.VuScheduleRef == filters.VUScheduleRef.Value);

            //Filter by traffic counts
            if (filters.FootTrafficCount.HasValue)
                q = q.Where(t => t.FootTrafficCount >= filters.FootTrafficCount.Value);
            //Filter by traffic counts
            if (filters.VehicleCount.HasValue)
                q = q.Where(t => t.VehicleCount >= filters.VehicleCount.Value);

            if (from.HasValue)
                q = q.Where(t => t.Timestamp >= from.Value);

            if (to.HasValue)
                q = q.Where(t => t.Timestamp <= to.Value);

            return q.OrderByDescending(t => t.Timestamp).ToList();
        }
    }
}
