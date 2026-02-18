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

    // traffic filter model
    public class TrafficFilterModel
    {
        public int? SensorId { get; set; }

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public string? MovementType { get; set; }
        public string? Direction { get; set; }
        public string? Season { get; set; }

        public bool? PublicTransportRef { get; set; }
        public bool? VUScheduleRef { get; set; }

        // alternate casing property
        [NotMapped]
        public bool? VuScheduleRef
        {
            get { return VUScheduleRef; }
            set { VUScheduleRef = value; }
        }

        public int? FootTrafficCount { get; set; }
        public int? VehicleCount { get; set; }

        public string? Zone { get; set; }
        public string? HeatmapPeriod { get; set; }

        public DateTime? TimeStamp { get; set; }
        public DateTime? TimeStampStart { get; set; }
        public DateTime? TimeStampEnd { get; set; }

        public string? ExportFormat { get; set; }

        public int Page { get; set; } = 1;

        // default page size
        public int PageSize { get; set; } = 25;
    }
}

namespace SmartTrafficMonitor.Services
{
    using SmartTrafficMonitor.Models;

    // paged result wrapper
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

    // heatmap URL generator
    public static class HeatmapService
    {
        public static string GenerateHeatmap(string? zone, string? period)
        {
            zone = zone ?? "";
            period = period ?? "";

            return $"/Heatmap/View?zone={Uri.EscapeDataString(zone)}&period={Uri.EscapeDataString(period)}";
        }
    }

    // export service class
    public static class ExportService
    {
        // CSV export builder
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

        // pdf export placeholder
        public static byte[] GeneratePdf(List<TrafficData> data)
        {
            return Array.Empty<byte>();
        }
    }

    // data access service
    public static class DataService
    {
        // full filtered list
        public static List<TrafficData> GetFilteredData(ApplicationDbContext context, TrafficFilterModel? filters)
        {
            var q = BuildFilteredQuery(context, filters);

            return q
                .OrderByDescending(t => t.Timestamp)
                .ToList();
        }

        //paged filtered list
        public static PagedResult<TrafficData> GetFilteredDataPaged(ApplicationDbContext context, TrafficFilterModel? filters)
        {
            filters ??= new TrafficFilterModel();

            if (filters.Page <= 0)
                filters.Page = 1;

            if (filters.PageSize != 25 && filters.PageSize != 50 && filters.PageSize != 100)
                filters.PageSize = 25;

            if (filters.PageSize > 100)
                filters.PageSize = 100;

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

        //Shared filtering logic
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
