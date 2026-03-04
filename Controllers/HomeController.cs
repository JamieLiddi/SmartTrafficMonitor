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

            // SensorId is now a string slug (Postgres text). Normalize it.
            if (!string.IsNullOrWhiteSpace(filters.SensorId))
            {
                filters.SensorId = filters.SensorId.Trim();
            }
            else
            {
                filters.SensorId = null;
            }

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

            var vm = new DashboardViewModel
            {
                Filters = filters,
                Results = paged.Items,

                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
                TotalPages = paged.TotalPages,

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

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }

    [ApiController]
    [Route("api")]
    public class HeatmapApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HeatmapApiController> _logger;
        private readonly IAuditLogService _audit;

        public HeatmapApiController(ApplicationDbContext context, ILogger<HeatmapApiController> logger, IAuditLogService audit)
        {
            _context = context;
            _logger = logger;
            _audit = audit;
        }

        [HttpGet("heatmap")]
        public IActionResult GetHeatmapData([FromQuery] TrafficFilterModel filters)
        {
            filters = filters ?? new TrafficFilterModel();

            try
            {
                var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);

                _logger.LogInformation(
                    "Generated heatmap for Zone: {Zone}, Period: {Period}",
                    filters.Zone, filters.HeatmapPeriod
                );

                return Redirect(heatmapUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving heatmap data");
                return StatusCode(500, "Internal server error while retrieving heatmap data.");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        {
            filters = filters ?? new TrafficFilterModel();

            var userEmail = User?.Identity?.Name;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrWhiteSpace(filters.ExportFormat))
            {
                _audit.Log("export", "missing format", false, userEmail, ip);
                return BadRequest("Export format is required!");
            }

            List<TrafficData> data;

            try
            {
                data = DataService.GetFilteredData(_context, filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data");
                _audit.Log("export", "query failed", false, userEmail, ip);
                return StatusCode(500, "Internal server error while exporting data.");
            }

            var details =
                $"format={filters.ExportFormat}, rows={data.Count}, sensorId={filters.SensorId}, from={filters.From}, to={filters.To}, movement={filters.MovementType}, direction={filters.Direction}, season={filters.Season}";

            switch (filters.ExportFormat.Trim().ToLowerInvariant())
            {
                case "csv":
                {
                    var csv = ExportService.GenerateCsv(data, filters, userEmail);
                    _logger.LogInformation("CSV export generated. Rows={RowCount}", data.Count);

                    _audit.Log("export_csv", details, true, userEmail, ip);

                    return File(csv, "text/csv; charset=utf-8", "traffic_report.csv");
                }

                case "pdf":
                {
                    var pdf = ExportService.GeneratePdf(data, filters, userEmail);

                    if (pdf == null || pdf.Length == 0)
                    {
                        _logger.LogWarning("PDF export requested but not implemented yet.");
                        _audit.Log("export_pdf", "pdf not implemented", false, userEmail, ip);
                        return StatusCode(501, "PDF export not implemented yet.");
                    }

                    _logger.LogInformation("PDF export generated. Rows={RowCount}", data.Count);

                    _audit.Log("export_pdf", details, true, userEmail, ip);

                    return File(pdf, "application/pdf", "traffic_report.pdf");
                }

                default:
                    _audit.Log("export", "unsupported format", false, userEmail, ip);
                    return BadRequest("Unsupported export format. Use csv or pdf.");
            }
        }
    }
}