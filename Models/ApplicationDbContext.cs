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
            base.OnModelCreating(modelBuilder);

            // Map TrafficData entity to the existing "traffictable" table
            modelBuilder.Entity<TrafficData>().ToTable("traffictable", "public");
            modelBuilder.Entity<TrafficData>(entity =>
        {
            entity.ToTable("traffictable", "public"); 
            entity.HasKey(t => t.Id);                 // Id = row primary key
            entity.Property(t => t.SensorId)
                .ValueGeneratedNever();          
        });
    }
}