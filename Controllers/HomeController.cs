using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;
using Microsoft.Extensions.Logging;

namespace SmartTrafficMonitor.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger; //Log dashboard actions

        // Inject DbContext via constructor
        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Accept filters from the querystring
        public IActionResult Index([FromQuery] TrafficFilterModel filters)
        {
            filters = filters ?? new TrafficFilterModel(); 
             if (filters.SensorId == 0)
                filters.SensorId = null;
            List<TrafficData> results;

            try
            {
                results = DataService.GetFilteredData(_context, filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving filtered data for dashboard");
                results = new List<TrafficData>(); // Fallback empty list on error

            }
            var vm = new DashboardViewModel
            {
                Filters = filters,
                Results = results
            };

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
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [ApiController]
    [Route("api")]
    public class HeatmapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HeatmapController> _logger; // Log heatmap and export actions

        public HeatmapController(ApplicationDbContext context, ILogger<HeatmapController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("heatmap")]
        public IActionResult GetHeatmapData([FromQuery] TrafficFilterModel filters)
        {
            filters = filters ?? new TrafficFilterModel(); 

            try
            {
             var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);
             _logger.LogInformation("Generated heatmap for Zone: {Zone}, Period: {Period}", filters.Zone, filters.HeatmapPeriod);
             return Redirect(heatmapUrl);   
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving heatmap data");
                return StatusCode(500, "Internal server error while retrieving heatmap data.");
            }
        }




        [HttpGet("export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        {
        filters = filters ?? new TrafficFilterModel(); 

        if (string.IsNullOrWhiteSpace(filters.ExportFormat))
        return BadRequest("Export format is required!");
        List<TrafficData> data;
            try
            {
                data = DataService.GetFilteredData(_context,filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data");
                return StatusCode(500, "Internal server error while exporting data.");
            }
            switch (filters.ExportFormat.Trim().ToLowerInvariant())
            {
                        case "csv":
        {
            var csv = ExportService.GenerateCsv(data);

            _logger.LogInformation("CSV export generated. Rows={RowCount}", data.Count);
            return File(csv, "text/csv; charset=utf-8", "traffic_report.csv");
        }

        case "pdf":
        {
            var pdf = ExportService.GeneratePdf(data);

            if (pdf == null || pdf.Length == 0)
            {
                _logger.LogWarning("PDF export requested but not implemented yet.");
                return StatusCode(501, "PDF export not implemented yet.");
            }

            _logger.LogInformation("PDF export generated. Rows={RowCount}", data.Count);
            return File(pdf, "application/pdf", "traffic_report.pdf");
        }

        default:
            return BadRequest("Unsupported export format. Use csv or pdf.");
    }
}
            }
        }   

