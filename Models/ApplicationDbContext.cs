using Microsoft.EntityFrameworkCore;

namespace SmartTrafficMonitor.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for TrafficData table
        public DbSet<TrafficData> TrafficDatas { get; set; }
    }
}
