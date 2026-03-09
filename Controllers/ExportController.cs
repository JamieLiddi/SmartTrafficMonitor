using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/export")]
    public class ExportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExportController> _logger;

        public ExportController(ApplicationDbContext context, ILogger<ExportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index([FromQuery] TrafficFilterModel filters)
        {
            filters ??= new TrafficFilterModel();

            // Clean up empty values from the dashboard form
            static string? Norm(string? value)
            {
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            filters.SensorId = Norm(filters.SensorId);
            filters.MovementType = Norm(filters.MovementType);
            filters.Direction = Norm(filters.Direction);
            filters.Season = Norm(filters.Season);
            filters.Zone = Norm(filters.Zone);
            filters.HeatmapPeriod = Norm(filters.HeatmapPeriod);

            var format = string.IsNullOrWhiteSpace(filters.ExportFormat)
                ? "csv"
                : filters.ExportFormat.Trim().ToLowerInvariant();

            try
            {
                // Export the full filtered dataset based on the current dashboard filters
                // Pagination is ignored so the full matching result set is exported
                var data = DataService.GetFilteredData(_context, filters);

                var generatedBy =
                    User?.Identity?.IsAuthenticated == true
                        ? User.Identity!.Name
                        : "unknown-admin";

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

                if (format == "pdf")
                {
                    var pdfBytes = ExportService.GeneratePdf(data, filters, generatedBy);

                    return File(
                        pdfBytes,
                        "application/pdf",
                        $"smart-traffic-monitor-export_{timestamp}.pdf"
                    );
                }

                var csvBytes = ExportService.GenerateCsv(data, filters, generatedBy);

                return File(
                    csvBytes,
                    "text/csv",
                    $"smart-traffic-monitor-export_{timestamp}.csv"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard export failed");
                return StatusCode(500, "Dashboard export failed. Please try again.");
            }
        }
    }
}