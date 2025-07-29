using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
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
        [HttpGet("heatmap")]
        public IActionResult GetHeatmap([FromQuery] TrafficFilterModel filters)
        {
            var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);
            return View("HeatmapView", model: heatmapUrl); // Create a Razor view that embeds the heatmap
        }

        [HttpGet("export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        { 
            var data = DataService.GetFilteredData(filters);

            if (filters.ExportFormat.ToLower() == "csv")
            {
                var csvFile = ExportService.GenerateCsv(data);
                return File(csvFile.Content, "text/csv", "traffic_report.csv");
            }
            else if (filters.ExportFormat.ToLower() == "pdf")
            {
                var pdfFile = ExportService.GeneratePdf(data);
                return File(pdfFile.Content, "application/pdf", "traffic_report.pdf");
            }
            return BadRequest("Unsupported export format.");          
        }
    }
}
