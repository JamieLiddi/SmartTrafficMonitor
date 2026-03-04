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
        public IActionResult Index(string? zone, string? date, string? sensor)
        {
            var scenario = new EventImpactScenario();

            // Default Date: use latest DB timestamp so baseline window isn't empty
            var latestUtc = _context.TrafficDatas
                .AsNoTracking()
                .OrderByDescending(t => t.Timestamp)
                .Select(t => (DateTime?)t.Timestamp)
                .FirstOrDefault();

            if (latestUtc.HasValue)
            {
                scenario.Date = latestUtc.Value.ToLocalTime().Date;
            }

            // Optional prefill from querystring
            if (!string.IsNullOrWhiteSpace(zone))
                scenario.Zone = zone;

            if (!string.IsNullOrWhiteSpace(sensor))
                scenario.SensorId = sensor;

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
            // Zones dropdown (only zones that actually exist in SensorLocations)
            var zones = _context.SensorLocations.AsNoTracking()
                .Where(z => z.Zone != null && z.Zone != "")
                .Select(z => z.Zone!)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            zones.Insert(0, "All");

            if (string.IsNullOrWhiteSpace(scenario.Zone) || !zones.Contains(scenario.Zone))
                scenario.Zone = "All";

            // ✅ Sensors dropdown from TrafficDatas (complete list)
            // If a zone is chosen, filter sensors by that zone using SensorLocations join.
            IQueryable<string> sensorsQuery;

            if (scenario.Zone == "All")
            {
                sensorsQuery = _context.TrafficDatas.AsNoTracking()
                    .Select(t => t.SensorId)
                    .Where(id => id != null && id != "")
                    .Distinct();
            }
            else
            {
                // Only sensors that have a SensorLocation AND match the chosen zone
                sensorsQuery =
                    (from t in _context.TrafficDatas.AsNoTracking()
                     join sl in _context.SensorLocations.AsNoTracking()
                         on t.SensorId equals sl.SensorSlug
                     where sl.Zone == scenario.Zone
                     select t.SensorId)
                    .Where(id => id != null && id != "")
                    .Distinct();
            }

            var sensors = sensorsQuery
                .OrderBy(s => s)
                .ToList();

            sensors.Insert(0, "All");

            if (string.IsNullOrWhiteSpace(scenario.SensorId) || !sensors.Contains(scenario.SensorId))
                scenario.SensorId = "All";

            return new EventImpactViewModel
            {
                Scenario = scenario,
                AvailableZones = zones,
                AvailableSensors = sensors
            };
        }
    }
}