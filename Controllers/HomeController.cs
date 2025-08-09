using System.Diagnostics;
using System.Linq;
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

        public IActionResult Index()
        {
            // Query traffic data from database
            var trafficDataList = _context.TrafficDatas.ToList();

            // Pass the data to the view (you need to create a view that accepts this)
            return View(trafficDataList);
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

    [Route("api")]
    public class HeatmapController : Controller
    {
        [HttpGet("heatmap")]
        public IActionResult GetHeatmap([FromQuery] TrafficFilterModel filters)
        {
            var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);
            return Redirect(heatmapUrl);
        }

        [HttpGet("export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        {
            if (string.IsNullOrEmpty(filters.ExportFormat))
                return BadRequest("ExportFormat is required.");

            var exportFormat = filters.ExportFormat.ToLower();

            var data = DataService.GetFilteredData(filters);

            if (exportFormat == "csv")
            {
                var csv = ExportService.GenerateCsv(data);
                return File(csv, "text/csv", "traffic_report.csv");
            }
            else if (exportFormat == "pdf")
            {
                var pdf = ExportService.GeneratePdf(data);
                return File(pdf, "application/pdf", "traffic_report.pdf");
            }
            else
            {
                return BadRequest("Unsupported export format.");
            }
        }
    }
}
