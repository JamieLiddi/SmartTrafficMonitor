using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;                  // Where(), OrderBy(), ToList()
using Microsoft.EntityFrameworkCore;

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

        // No such column in DB — keep as a transient property if you want it in the UI
        [NotMapped]
        public string Direction { get; set; }

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

    // View/filter model (NOT an EF entity)
    public class TrafficFilterModel
    {
        // From your UI
        public int? SensorId { get; set; }
        public DateTime? From { get; set; }          // matches Index.cshtml
        public DateTime? To { get; set; }            // matches Index.cshtml

        public string MovementType { get; set; }     // Pedestrian/Vehicle/Cyclist/empty
        public string Direction { get; set; }        // UI-only (no DB column)
        public string Season { get; set; }           // Summer/Autumn/Winter/Spring/empty
        public bool? PublicTransportRef { get; set; }
        public bool? VUScheduleRef { get; set; }     // UI uses VU..., entity uses Vu...

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
        public static List<TrafficData> GetFilteredData(ApplicationDbContext context, TrafficFilterModel filters)
        {
            var q = context.TrafficDatas.AsQueryable();

            if (filters == null)
                return q.OrderByDescending(t => t.Timestamp).ToList();

            if (filters.SensorId.HasValue)
                q = q.Where(t => t.SensorId == filters.SensorId.Value);

            if (!string.IsNullOrWhiteSpace(filters.MovementType))
                q = q.Where(t => t.MovementType == filters.MovementType);

            // Direction filter removed — no column in DB

            if (!string.IsNullOrWhiteSpace(filters.Season))
                q = q.Where(t => t.Season == filters.Season);

            if (filters.PublicTransportRef.HasValue)
                q = q.Where(t => t.PublicTransportRef == filters.PublicTransportRef.Value);

            if (filters.VUScheduleRef.HasValue)
                q = q.Where(t => t.VuScheduleRef == filters.VUScheduleRef.Value);

            if (filters.FootTrafficCount.HasValue)
                q = q.Where(t => t.FootTrafficCount >= filters.FootTrafficCount.Value);

            if (filters.VehicleCount.HasValue)
                q = q.Where(t => t.VehicleCount >= filters.VehicleCount.Value);

            // Date range: prefer From/To; fall back to TimeStampStart/End
            var from = filters.From ?? filters.TimeStampStart ?? filters.TimeStamp;
            var to   = filters.To   ?? filters.TimeStampEnd   ?? filters.TimeStamp;

            if (from.HasValue)
                q = q.Where(t => t.Timestamp >= from.Value);

            if (to.HasValue)
                q = q.Where(t => t.Timestamp <= to.Value);

            return q.OrderByDescending(t => t.Timestamp).ToList();
        }
    }
}
