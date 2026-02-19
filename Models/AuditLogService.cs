using System;
using SmartTrafficMonitor.Models;

namespace SmartTrafficMonitor.Services
{
    public interface IAuditLogService
    {
     void Log(string action, string? details, bool success, string? userEmail, string? ip);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;
        public AuditLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void Log(string action, string? details, bool success, string? userEmail, string? ip)
        {
            var row = new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                Action = action ?? "",
                Details = details,
                Success = success,
                UserEmail = userEmail,
                Ip = ip
            };
            _context.AuditLogs.Add(row);
            _context.SaveChanges();
        }
    }
}
