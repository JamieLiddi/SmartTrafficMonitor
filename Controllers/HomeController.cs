using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index([FromQuery] TrafficFilterModel filters)
        {
            filters = filters ?? new TrafficFilterModel();

            if (!string.IsNullOrWhiteSpace(filters.SensorId))
                filters.SensorId = filters.SensorId.Trim();
            else
                filters.SensorId = null;

            if (filters.Page <= 0)
                filters.Page = 1;

            if (filters.PageSize != 25 && filters.PageSize != 50 && filters.PageSize != 100)
                filters.PageSize = 25;

            var hasAnyQueryFilters = Request?.Query != null && Request.Query.Count > 0;

            if (!hasAnyQueryFilters)
            {
                var latestTs = _context.TrafficDatas
                    .Select(t => (DateTime?)t.Timestamp)
                    .Max();

                var anchor = latestTs ?? DateTime.UtcNow;

                filters.Page = 1;
                filters.From = anchor.AddDays(-7);
                filters.To = anchor;
            }

            PagedResult<TrafficData> paged;

            try
            {
                paged = DataService.GetFilteredDataPaged(_context, filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data");

                paged = new PagedResult<TrafficData>
                {
                    Items = new List<TrafficData>(),
                    TotalCount = 0,
                    Page = filters.Page,
                    PageSize = filters.PageSize
                };
            }

            var sensors = _context.TrafficDatas
                .Select(t => t.SensorId)
                .Where(id => id != null && id != "")
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            sensors.Insert(0, "");

            // ✅ KPIs
            long kpiTotalFoot = 0;
            long kpiTotalCyclists = 0;   // ✅ NEW
            long kpiTotalVeh = 0;
            long kpiRecordCount = 0;
            string kpiBusiestSensor = "—";
            string kpiPeakHour = "—";

            try
            {
                // Records = same filters as the table
                var tableQuery = DataService.GetFilteredQuery(_context, filters);
                kpiRecordCount = tableQuery.LongCount();

                // Base KPI filters ignore MovementType (so KPIs don’t zero each other)
                var baseKpiFilters = new TrafficFilterModel
                {
                    SensorId = filters.SensorId,
                    From = filters.From,
                    To = filters.To,
                    Direction = filters.Direction,
                    Season = filters.Season,
                    PublicTransportRef = filters.PublicTransportRef,
                    VUScheduleRef = filters.VUScheduleRef,
                    FootTrafficCount = filters.FootTrafficCount,
                    VehicleCount = filters.VehicleCount,
                    Zone = filters.Zone,
                    HeatmapPeriod = filters.HeatmapPeriod,
                    TimeStamp = filters.TimeStamp,
                    TimeStampStart = filters.TimeStampStart,
                    TimeStampEnd = filters.TimeStampEnd
                };

                var pedFilters = new TrafficFilterModel
                {
                    SensorId = baseKpiFilters.SensorId,
                    From = baseKpiFilters.From,
                    To = baseKpiFilters.To,
                    Direction = baseKpiFilters.Direction,
                    Season = baseKpiFilters.Season,
                    PublicTransportRef = baseKpiFilters.PublicTransportRef,
                    VUScheduleRef = baseKpiFilters.VUScheduleRef,
                    FootTrafficCount = baseKpiFilters.FootTrafficCount,
                    VehicleCount = baseKpiFilters.VehicleCount,
                    Zone = baseKpiFilters.Zone,
                    HeatmapPeriod = baseKpiFilters.HeatmapPeriod,
                    TimeStamp = baseKpiFilters.TimeStamp,
                    TimeStampStart = baseKpiFilters.TimeStampStart,
                    TimeStampEnd = baseKpiFilters.TimeStampEnd,
                    MovementType = "Pedestrian"
                };

                var cycFilters = new TrafficFilterModel
                {
                    SensorId = baseKpiFilters.SensorId,
                    From = baseKpiFilters.From,
                    To = baseKpiFilters.To,
                    Direction = baseKpiFilters.Direction,
                    Season = baseKpiFilters.Season,
                    PublicTransportRef = baseKpiFilters.PublicTransportRef,
                    VUScheduleRef = baseKpiFilters.VUScheduleRef,
                    FootTrafficCount = baseKpiFilters.FootTrafficCount,
                    VehicleCount = baseKpiFilters.VehicleCount,
                    Zone = baseKpiFilters.Zone,
                    HeatmapPeriod = baseKpiFilters.HeatmapPeriod,
                    TimeStamp = baseKpiFilters.TimeStamp,
                    TimeStampStart = baseKpiFilters.TimeStampStart,
                    TimeStampEnd = baseKpiFilters.TimeStampEnd,
                    MovementType = "Cyclist"
                };

                var vehFilters = new TrafficFilterModel
                {
                    SensorId = baseKpiFilters.SensorId,
                    From = baseKpiFilters.From,
                    To = baseKpiFilters.To,
                    Direction = baseKpiFilters.Direction,
                    Season = baseKpiFilters.Season,
                    PublicTransportRef = baseKpiFilters.PublicTransportRef,
                    VUScheduleRef = baseKpiFilters.VUScheduleRef,
                    FootTrafficCount = baseKpiFilters.FootTrafficCount,
                    VehicleCount = baseKpiFilters.VehicleCount,
                    Zone = baseKpiFilters.Zone,
                    HeatmapPeriod = baseKpiFilters.HeatmapPeriod,
                    TimeStamp = baseKpiFilters.TimeStamp,
                    TimeStampStart = baseKpiFilters.TimeStampStart,
                    TimeStampEnd = baseKpiFilters.TimeStampEnd,
                    MovementType = "Vehicle"
                };

                var pedQuery = DataService.GetFilteredQuery(_context, pedFilters);
                var cycQuery = DataService.GetFilteredQuery(_context, cycFilters);
                var vehQuery = DataService.GetFilteredQuery(_context, vehFilters);

                // ✅ Pedestrians: FootTrafficCount on Pedestrian rows
                kpiTotalFoot = pedQuery.Select(x => (long)x.FootTrafficCount).Sum();

                // ✅ Cyclists: FootTrafficCount on Cyclist rows (matches your DB reality)
                kpiTotalCyclists = cycQuery.Select(x => (long)x.FootTrafficCount).Sum();

                // ✅ Vehicles: VehicleCount on Vehicle rows
                kpiTotalVeh = vehQuery.Select(x => (long)x.VehicleCount).Sum();

                // Busiest + peak hour use combined volume across ALL movement types
                var combinedQuery = DataService.GetFilteredQuery(_context, baseKpiFilters);

                var busiest = combinedQuery
                    .GroupBy(x => x.SensorId)
                    .Select(g => new
                    {
                        SensorId = g.Key,
                        Total = g.Sum(x => (long)x.FootTrafficCount) + g.Sum(x => (long)x.VehicleCount)
                    })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                if (busiest != null && !string.IsNullOrWhiteSpace(busiest.SensorId))
                    kpiBusiestSensor = busiest.SensorId;

                var peak = combinedQuery
                    .GroupBy(x => x.Timestamp.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Total = g.Sum(x => (long)x.FootTrafficCount) + g.Sum(x => (long)x.VehicleCount)
                    })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                if (peak != null)
                {
                    var start = new DateTime(2000, 1, 1, peak.Hour, 0, 0);
                    var end = start.AddHours(1);
                    kpiPeakHour = $"{start:htt}–{end:htt}".Replace(" ", "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing KPI values");
            }

            var vm = new DashboardViewModel
            {
                Filters = filters,
                Results = paged.Items,

                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
                TotalPages = paged.TotalPages,

                AvailableSensors = sensors,

                // KPIs
                KpiTotalFootTraffic = kpiTotalFoot,
                KpiTotalCyclists = kpiTotalCyclists,   // ✅ NEW
                KpiTotalVehicles = kpiTotalVeh,
                KpiRecordCount = kpiRecordCount,
                KpiBusiestSensor = kpiBusiestSensor,
                KpiPeakHour = kpiPeakHour,

                ShowFallbackWarning = false,
                FallbackMessage = ""
            };

            if (vm.Results != null && vm.Results.Count == 0 && filters.From.HasValue && filters.To.HasValue)
            {
                try
                {
                    var lastKnown = _context.TrafficDatas
                        .OrderByDescending(t => t.Timestamp)
                        .Take(filters.PageSize)
                        .ToList();

                    if (lastKnown.Count > 0)
                    {
                        vm.Results = lastKnown;
                        vm.TotalCount = lastKnown.Count;
                        vm.TotalPages = 1;
                        vm.Page = 1;

                        vm.ShowFallbackWarning = true;
                        vm.FallbackMessage = "Showing last known data. Real-time feed may be unavailable for the selected window.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving fallback data");
                }
            }

            return View(vm);
        }

        public IActionResult About() => View();
        public IActionResult Contact() => View();

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}