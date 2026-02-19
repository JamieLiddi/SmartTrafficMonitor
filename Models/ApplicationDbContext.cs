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
        // DbSet for AuditLog model
        public DbSet<AuditLog> AuditLogs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Always call base first so EF conventions are applied
            base.OnModelCreating(modelBuilder);

            //Map TrafficData entity to existing "traffictable" table + schema
            modelBuilder.Entity<TrafficData>().ToTable("traffictable", "public");
            modelBuilder.Entity<TrafficData>().HasKey(t => t.Id); // Id = Primary Key

            //SensorId is NOT auto-generated (it’s a sensor identifier)
            modelBuilder.Entity<TrafficData>()
                .Property(t => t.SensorId)
                .ValueGeneratedNever();
        }
    }
}
