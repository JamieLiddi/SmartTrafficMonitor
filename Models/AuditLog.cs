using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTrafficMonitor.Models
{
    [Table("auditlog", Schema = "public")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
           public int Id { get; set; }

        // Store timestamps in UTC to avoid timezone issues
        [Column("TimestampUtc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        [Column("UserEmail")]
        public string? UserEmail { get; set; }
        [Column("Action")]
        public string Action { get; set; } = "";
        [Column("Details")]
        public string? Details { get; set; }
        [Column("Success")]
        public bool Success { get; set; } = true;
        [Column("Ip")]
        public string? Ip { get; set; }
    }
}
