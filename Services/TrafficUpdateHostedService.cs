using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public class TrafficUpdateHostedService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _env;
        private Timer? _timer;

        public TrafficUpdateHostedService(IServiceScopeFactory scopeFactory, IHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _env = env;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(Run, null, TimeSpan.Zero, TimeSpan.FromMinutes(15));
            return Task.CompletedTask;
        }

        private void Run(object? state)
        {
            using var scope = _scopeFactory.CreateScope();

            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                // 1) Prefer env vars if you set them
                var ped = Environment.GetEnvironmentVariable("CSV_PED_FOLDER");
                var veh = Environment.GetEnvironmentVariable("CSV_VEH_FOLDER");
                var cyc = Environment.GetEnvironmentVariable("CSV_CYC_FOLDER");

                // 2) Fallback: autodetect folders in repo / content root
                if (string.IsNullOrWhiteSpace(ped) || string.IsNullOrWhiteSpace(veh) || string.IsNullOrWhiteSpace(cyc))
                {
                    var root = _env.ContentRootPath;

                    // Try common variants (your repo uses spaces + capitals)
                    ped ??= FirstExistingDir(root,
                        Path.Combine("data", "Pedestrian Count"),
                        Path.Combine("data", "pedestrian_count"),
                        Path.Combine("data", "pedestrian"),
                        Path.Combine("data", "Pedestrian"));

                    veh ??= FirstExistingDir(root,
                        Path.Combine("data", "Vehicle Count"),
                        Path.Combine("data", "vehicle_count"),
                        Path.Combine("data", "vehicle"),
                        Path.Combine("data", "Vehicle"));

                    cyc ??= FirstExistingDir(root,
                        Path.Combine("data", "Cyclist Count"),
                        Path.Combine("data", "cyclist_count"),
                        Path.Combine("data", "cyclist"),
                        Path.Combine("data", "Cyclist"));
                }

                Console.WriteLine("[IMPORT] Starting CSV import...");
                Console.WriteLine($"[IMPORT] ContentRootPath: {_env.ContentRootPath}");
                Console.WriteLine($"[IMPORT] Pedestrian folder: {ped}");
                Console.WriteLine($"[IMPORT] Vehicle folder:    {veh}");
                Console.WriteLine($"[IMPORT] Cyclist folder:    {cyc}");

                var imported = CsvImportService.ImportFromFolders(context, audit, ped, veh, cyc);
                Console.WriteLine($"[IMPORT] Completed. Rows imported = {imported}");

                var backupKey = Environment.GetEnvironmentVariable("BACKUP_AES_KEY_BASE64");
                BackupService.RunDailyBackup(context, audit, backupKey);

                audit.Log("scheduler_tick", $"15 min job ran. imported={imported}", true, null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IMPORT] FAILED: " + ex);
                audit.Log("scheduler_tick", "15 min job failed: " + ex.Message, false, null, null);
            }
        }

        private static string? FirstExistingDir(string root, params string[] relativeCandidates)
        {
            foreach (var rel in relativeCandidates)
            {
                var full = Path.Combine(root, rel);
                if (Directory.Exists(full))
                    return full;
            }
            return null;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}