using System;
using System.Globalization;
using System.Linq;
using System.Text;
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

            // 1) Default Date: use latest DB timestamp so baseline window isn't empty
            var latestUtc = _context.TrafficDatas
                .AsNoTracking()
                .OrderByDescending(t => t.Timestamp)
                .Select(t => (DateTime?)t.Timestamp)
                .FirstOrDefault();

            if (latestUtc.HasValue)
            {
                scenario.Date = latestUtc.Value.ToLocalTime().Date;
            }

            // 2) Optional prefill from querystring
            if (!string.IsNullOrWhiteSpace(zone))
                scenario.Zone = zone;

            if (!string.IsNullOrWhiteSpace(sensor))
                scenario.SensorId = sensor;

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
                scenario.Date = parsedDate.Date;

            // ✅ Sensor overrides zone (matches UI/help text)
            if (!string.IsNullOrWhiteSpace(scenario.SensorId) && scenario.SensorId != "All")
            {
                scenario.Zone = "All";
            }

            var vm = BuildVm(scenario);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(EventImpactScenario scenario)
        {
            // ✅ Sensor overrides zone (matches UI/help text)
            if (!string.IsNullOrWhiteSpace(scenario.SensorId) && scenario.SensorId != "All")
            {
                scenario.Zone = "All";
            }

            var vm = BuildVm(scenario);

            if (!ModelState.IsValid)
                return View(vm);

            vm.Results = _service.ProjectHourly(scenario);

            // ✅ Totals should match the wording "scenario window"
            var windowRows = vm.Results.Where(x => x.IsImpacted).ToList();

            // Safety fallback: if the window selects nothing, use full day
            if (windowRows.Count == 0)
                windowRows = vm.Results;

            vm.TotalBaseline = windowRows.Sum(x => x.Baseline);
            vm.TotalProjected = windowRows.Sum(x => x.Projected);
            vm.Delta = vm.TotalProjected - vm.TotalBaseline;

            // Percent change (avoid divide-by-zero)
            vm.PercentChange = vm.TotalBaseline == 0
                ? 0
                : (vm.Delta / vm.TotalBaseline) * 100.0;

            return View(vm);
        }

        // ✅ Export CSV endpoint used by: /EventImpact/ExportCsv?...querystring...
        [HttpGet]
        public IActionResult ExportCsv(
            string? zone,
            string? sensor,
            string? date,
            string? overrideDayOfWeek,
            ProjectionMovement movement,
            int lookbackWeeks = 12,
            double uncertaintyPercent = 15,
            bool hasEvent = false,
            double eventUpliftPercent = 0,
            bool hasVuImpact = false,
            double vuUpliftPercent = 0,
            int startHour = 12,
            int durationHours = 6
        )
        {
            // ✅ FIX: OverrideDayOfWeek is DayOfWeek? (NOT string)
            DayOfWeek? parsedDow = null;
            if (!string.IsNullOrWhiteSpace(overrideDayOfWeek) &&
                Enum.TryParse<DayOfWeek>(overrideDayOfWeek, true, out var dow))
            {
                parsedDow = dow;
            }

            var scenario = new EventImpactScenario
            {
                Zone = string.IsNullOrWhiteSpace(zone) ? "All" : zone,
                SensorId = string.IsNullOrWhiteSpace(sensor) ? "All" : sensor,
                Movement = movement,
                LookbackWeeks = lookbackWeeks,
                UncertaintyPercent = uncertaintyPercent,
                HasEvent = hasEvent,
                EventUpliftPercent = eventUpliftPercent,
                HasVuImpact = hasVuImpact,
                VuUpliftPercent = vuUpliftPercent,
                StartHour = startHour,
                DurationHours = durationHours,
                OverrideDayOfWeek = parsedDow
            };

            // ✅ Sensor overrides zone (keep consistent everywhere)
            if (!string.IsNullOrWhiteSpace(scenario.SensorId) && scenario.SensorId != "All")
                scenario.Zone = "All";

            // Parse date if provided, otherwise use latest DB timestamp date
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
            {
                scenario.Date = parsed.Date;
            }
            else
            {
                var latestUtc = _context.TrafficDatas
                    .AsNoTracking()
                    .OrderByDescending(t => t.Timestamp)
                    .Select(t => (DateTime?)t.Timestamp)
                    .FirstOrDefault();

                scenario.Date = latestUtc.HasValue ? latestUtc.Value.ToLocalTime().Date : DateTime.Today;
            }

            var results = _service.ProjectHourly(scenario);

            // Build CSV (UTF-8 with BOM for Excel friendliness)
            var sb = new StringBuilder();
            sb.AppendLine("Hour,Baseline,Projected,Delta,Lower,Upper,InWindow");

            foreach (var r in results.OrderBy(x => x.Hour))
            {
                var delta = r.Projected - r.Baseline;

                sb.Append(r.Hour).Append(',')
                  .Append(r.Baseline.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Projected.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(delta.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Lower.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Upper.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.IsImpacted ? "Yes" : "No")
                  .AppendLine();
            }

            // UTF8 BOM so Excel opens it cleanly
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

            var safeZone = (scenario.Zone ?? "All").Replace(" ", "-");
            var safeSensor = (scenario.SensorId ?? "All").Replace(" ", "-");
            var fileName = $"event-impact_{scenario.Date:yyyyMMdd}_{scenario.Movement}_{safeZone}_{safeSensor}.csv";

            return File(bytes, "text/csv", fileName);
        }

        private EventImpactViewModel BuildVm(EventImpactScenario scenario)
        {
            // Zones dropdown (only zones that exist in SensorLocations)
            var zones = _context.SensorLocations.AsNoTracking()
                .Where(z => z.Zone != null && z.Zone != "")
                .Select(z => z.Zone!)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            zones.Insert(0, "All");

            if (string.IsNullOrWhiteSpace(scenario.Zone) || !zones.Contains(scenario.Zone))
                scenario.Zone = "All";

            // Sensors dropdown ALWAYS from TrafficDatas (complete list)
            var sensors = _context.TrafficDatas.AsNoTracking()
                .Select(t => t.SensorId)
                .Where(id => id != null && id != "")
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            sensors.Insert(0, "All");

            if (string.IsNullOrWhiteSpace(scenario.SensorId) || !sensors.Contains(scenario.SensorId))
                scenario.SensorId = "All";

            // ✅ If a sensor is selected, force Zone to All so the UI/controller stay consistent
            if (!string.IsNullOrWhiteSpace(scenario.SensorId) && scenario.SensorId != "All")
                scenario.Zone = "All";

            return new EventImpactViewModel
            {
                Scenario = scenario,
                AvailableZones = zones,
                AvailableSensors = sensors
            };
        }
    }
}