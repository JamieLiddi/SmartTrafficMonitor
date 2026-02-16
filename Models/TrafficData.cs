using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SmartTrafficMonitor.Models
{
    [Table("traffictable", Schema = "public")]
    public class TrafficData
    {
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("SensorId")]
        public int SensorId { get; set; }

        [Column("TimeStamp")]
        public DateTime Timestamp { get; set; }

        [Column("MovementType")]
        public string? MovementType { get; set; }

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

        public string? MovementType { get; set; }    // Pedestrian/Vehicle/Cyclist/empty
        public string? Direction { get; set; }       // North/South/East/West/empty
        public string? Season { get; set; }          // Summer/Autumn/Winter/Spring/empty

        public bool? PublicTransportRef { get; set; }
        public bool? VUScheduleRef { get; set; }

        // Some parts of your app use VuScheduleRef casing - keep compatibility
        [NotMapped]
        public bool? VuScheduleRef
        {
            get { return VUScheduleRef; }
            set { VUScheduleRef = value; }
        }

        public int? FootTrafficCount { get; set; }
        public int? VehicleCount { get; set; }

        // Heatmap inputs
        public string? Zone { get; set; }
        public string? HeatmapPeriod { get; set; }    // Weekly/Monthly/Seasonal

        // Legacy names (keep for compatibility if someone calls API with these)
        public DateTime? TimeStamp { get; set; }
        public DateTime? TimeStampStart { get; set; }
        public DateTime? TimeStampEnd { get; set; }

        // Export
        public string? ExportFormat { get; set; }     // "csv" | "pdf"

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;       // UI allows 50–100
    }
}

namespace SmartTrafficMonitor.Services
{
    using SmartTrafficMonitor.Models;

    // ONE paged result wrapper (keep it only here to avoid duplicates)
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalPages
        {
            get
            {
                if (PageSize <= 0) return 0;
                return (int)Math.Ceiling(TotalCount / (double)PageSize);
            }
        }
    }

    public static class HeatmapService
    {
        public static string GenerateHeatmap(string? zone, string? period)
        {
            // Stub implementation (controller/view renders actual map)
            zone = zone ?? "";
            period = period ?? "";
            return $"/Heatmap/View?zone={Uri.EscapeDataString(zone)}&period={Uri.EscapeDataString(period)}";
        }
    }

    public static class ExportService
    {
        public static byte[] GenerateCsv(List<TrafficData> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("sensor_id,timestamp,movement_type,direction,season,foot_traffic_count,vehicle_count,public_transport_ref,vu_schedule_ref");

            string Esc(string? s) => s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

            foreach (var r in data)
            {
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
        // ✅ Use this for EXPORT and any “full filtered dataset” use
        public static List<TrafficData> GetFilteredData(ApplicationDbContext context, TrafficFilterModel? filters)
        {
            var q = BuildFilteredQuery(context, filters);

            // Full list (no paging)
            return q
                .OrderByDescending(t => t.Timestamp)
                .ToList();
        }

        // ✅ Use this for DASHBOARD RESULTS TABLE (paged)
        public static PagedResult<TrafficData> GetFilteredDataPaged(ApplicationDbContext context, TrafficFilterModel? filters)
        {
            filters ??= new TrafficFilterModel();

            if (filters.Page <= 0) filters.Page = 1;
            if (filters.PageSize <= 0) filters.PageSize = 50;
            if (filters.PageSize > 100) filters.PageSize = 100;

            var q = BuildFilteredQuery(context, filters);

            var total = q.Count();

            var items = q
                .OrderByDescending(t => t.Timestamp)
                .Skip((filters.Page - 1) * filters.PageSize)
                .Take(filters.PageSize)
                .ToList();

            return new PagedResult<TrafficData>
            {
                Items = items,
                TotalCount = total,
                Page = filters.Page,
                PageSize = filters.PageSize
            };
        }

        // Shared filter logic so List + Paged stay consistent
        private static IQueryable<TrafficData> BuildFilteredQuery(ApplicationDbContext context, TrafficFilterModel? filters)
        {
            var q = context.TrafficDatas.AsQueryable();

            if (filters == null)
                return q;

            var from = filters.From ?? filters.TimeStampStart ?? filters.TimeStamp;
            var to = filters.To ?? filters.TimeStampEnd ?? filters.TimeStamp;

            if (from.HasValue)
                from = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            if (to.HasValue)
                to = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);

            if (filters.SensorId.HasValue)
                q = q.Where(t => t.SensorId == filters.SensorId.Value);

            if (!string.IsNullOrWhiteSpace(filters.MovementType))
                q = q.Where(t => t.MovementType == filters.MovementType);

            if (!string.IsNullOrWhiteSpace(filters.Direction))
                q = q.Where(t => t.Direction == filters.Direction);

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

            if (from.HasValue)
                q = q.Where(t => t.Timestamp >= from.Value);

            if (to.HasValue)
                q = q.Where(t => t.Timestamp <= to.Value);

            return q;
        }
    }
}
