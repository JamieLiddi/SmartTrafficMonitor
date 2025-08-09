using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Inject DbContext via constructor
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Accept filters from the querystring
        public IActionResult Index([FromQuery] TrafficFilterModel filters)
        {
            // Get data using the DataService (requires db + filters)
            var results = DataService.GetFilteredData(_context, filters);

            // Build a view model instead of putting results on the filter
            var vm = new DashboardViewModel
            {
                Filters = filters,
                Results = results
            };

            return View(vm);
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";
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

        public HeatmapController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("heatmap")]
        public IActionResult GetHeatmap([FromQuery] TrafficFilterModel filters)
        {
            var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);
            return Redirect(heatmapUrl);
        }

        [HttpGet("export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        {
            if (string.IsNullOrEmpty(filters?.ExportFormat))
                return BadRequest("ExportFormat is required.");

            var data = DataService.GetFilteredData(_context, filters);

            switch (filters.ExportFormat.ToLowerInvariant())
            {
                case "csv":
                    var csv = ExportService.GenerateCsv(data);
                    return File(csv, "text/csv", "traffic_report.csv");

                case "pdf":
                    var pdf = ExportService.GeneratePdf(data);
                    return File(pdf, "application/pdf", "traffic_report.pdf");

                default:
                    return BadRequest("Unsupported export format.");
            }
        }
    }
}
