using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

namespace SmartTrafficMonitor.Controllers
{
    public class EventImpactController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEventImpactProjectionService _service;

        public EventImpactController(ApplicationDbContext context, IEventImpactProjectionService service)
        {
            _context = context;
            _service = service;
        }

        [HttpGet]
        public IActionResult Index(string? zone, string? date)
        {
            var scenario = new EventImpactScenario();

            // 1) Default Date: use latest DB timestamp so baseline window isn't empty
            var latestUtc = _context.TrafficDatas
                .AsNoTracking()
                .OrderByDescending(t => t.Timestamp)
                .Select(t => (DateTime?)t.Timestamp)
                .FirstOrDefault();

            if (latestUtc.HasValue)
            {
                // Date picker is local-date based
                scenario.Date = latestUtc.Value.ToLocalTime().Date;
            }

            // 2) Optional prefill from querystring (e.g. /EventImpact?zone=Footscray%20Park&date=2026-03-01)
            if (!string.IsNullOrWhiteSpace(zone))
                scenario.Zone = zone;

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
                scenario.Date = parsedDate.Date;

            var vm = BuildVm(scenario);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(EventImpactScenario scenario)
        {
            var vm = BuildVm(scenario);

            if (!ModelState.IsValid)
                return View(vm);

            vm.Results = _service.ProjectHourly(scenario);

            vm.TotalBaseline = vm.Results.Sum(x => x.Baseline);
            vm.TotalProjected = vm.Results.Sum(x => x.Projected);
            vm.Delta = vm.TotalProjected - vm.TotalBaseline;

            return View(vm);
        }

        private EventImpactViewModel BuildVm(EventImpactScenario scenario)
        {
            var zones = _context.SensorLocations.AsNoTracking()
                .Where(z => z.Zone != null && z.Zone != "")
                .Select(z => z.Zone!)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            zones.Insert(0, "All");

            // If the scenario.Zone isn't in the dropdown (e.g., no sensor locations yet),
            // keep it stable by falling back to "All".
            if (string.IsNullOrWhiteSpace(scenario.Zone) || !zones.Contains(scenario.Zone))
                scenario.Zone = "All";

            return new EventImpactViewModel
            {
                Scenario = scenario,
                AvailableZones = zones
            };
        }
    }
}