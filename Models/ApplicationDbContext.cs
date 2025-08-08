using Microsoft.EntityFrameworkCore;

namespace SmartTrafficMonitor.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for TrafficData model
        public DbSet<TrafficData> TrafficDatas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map TrafficData entity to existing "traffictable" table
            modelBuilder.Entity<TrafficData>().ToTable("traffictable");

            base.OnModelCreating(modelBuilder);
        }
    }
}