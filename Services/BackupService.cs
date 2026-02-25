using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public static class BackupService
    {
        public static void RunDailyBackup(ApplicationDbContext context, IAuditLogService audit, string? keyBase64)
        {
            // Backup folder
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "backups");
            Directory.CreateDirectory(dir);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var plainPath = Path.Combine(dir, $"traffic_backup_{stamp}.csv");
            var encPath = plainPath + ".enc";

            // Avoid overwriting existing backup for the day
            if (File.Exists(encPath))
                return;

            // Pull recent data 
            var rows = context.TrafficDatas
                .OrderByDescending(t => t.Timestamp)
                .Take(50000) // limit to recent 50k rows for backup
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("sensor_id,timestamp,movement_type,direction,season,foot_traffic_count,vehicle_count,public_transport_ref,vu_schedule_ref");

            foreach (var r in rows)
            {
                sb.Append(r.SensorId).Append(',');
                sb.Append(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
                sb.Append(Esc(r.MovementType)).Append(',');
                sb.Append(Esc(r.Direction)).Append(',');
                sb.Append(Esc(r.Season)).Append(',');
                sb.Append(r.FootTrafficCount).Append(',');
                sb.Append(r.VehicleCount).Append(',');
                sb.Append(r.PublicTransportRef ? "true" : "false").Append(',');
                sb.AppendLine(r.VuScheduleRef ? "true" : "false");
            }

            File.WriteAllText(plainPath, sb.ToString(), Encoding.UTF8);

            // If no key provided, keep plaintext backup (but log a warning)
            if (string.IsNullOrWhiteSpace(keyBase64))
            {
                audit.Log("backup", "missing BACKUP_AES_KEY_BASE64, wrote plaintext backup only", false, null, null);
                return;
            }

            var key = Convert.FromBase64String(keyBase64);
            var plainBytes = File.ReadAllBytes(plainPath);
            var encBytes = EncryptAes(plainBytes, key);

            File.WriteAllBytes(encPath, encBytes);
            File.Delete(plainPath);

            audit.Log("backup", $"daily backup created: {Path.GetFileName(encPath)} rows={rows.Count}", true, null, null);
        }
        // Simple CSV escaping
        private static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        // AES-256 encryption with a random IV prefix
        private static byte[] EncryptAes(byte[] plain, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = key;
                aes.GenerateIV();

                using (var ms = new MemoryStream())
                {
                    // prefix IV
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(plain, 0, plain.Length);
                        cs.FlushFinalBlock();
                    }

                    return ms.ToArray();
                }
            }
        }
    }
}