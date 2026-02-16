using Microsoft.AspNetCore.Mvc;

namespace SmartTrafficMonitor.Controllers
{
    public class HeatmapPageController : Controller
    {
        public IActionResult View(string zone, string period)
        {
            ViewData["Zone"] = string.IsNullOrEmpty(zone) ? "Unknown Zone" : zone;
            ViewData["Period"] = string.IsNullOrEmpty(period) ? "Unknown Period" : period;

            return View();
        }
    }
}