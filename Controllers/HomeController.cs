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
    public class HeatmapController : Controller
    {
        [HttpGet]
        [Route("api/heatmap")]
        public IActionResult GetHeatmap([FromQuery] TrafficFilterModel filters)
        { //This would call service to generate a heatmap URL or HTML
            var heatmapUrl = HeatmapService.GenerateHeatmap(filters.Zone, filters.HeatmapPeriod);
            return Redirect(heatmapUrl); 
          // Or return View with embedded map
        }
        [HttpGet]
        [Route("api/export")]
        public IActionResult ExportData([FromQuery] TrafficFilterModel filters)
        { 
            var data = DataService.GetFilteredData(filters);

            if (filters.ExportFormat == "csv")
            {
                var csvFile = ExportService.GenerateCsv(data);
                return File(csvFile.Content, "text/csv", "traffic_report.csv");
            }
            else if (filters.ExportFormat == "pdf")
            {
                var pdfFile = ExportService.GeneratePdf(data);
                return File(pdfFile.Content, "application/pdf", "traffic_report.pdf");
            }
            return BadRequest("Unsupported export format.");          
        }
    }
}
