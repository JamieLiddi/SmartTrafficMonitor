using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTrafficMonitor.Models
{
    // Sensor location model
    [Table("sensor_locations", Schema = "public")]
    public class SensorLocation
    {
        [Key]
        [Column("SensorId")]
        public int SensorId { get; set; }

        [Column("Latitude")]
        public double Latitude { get; set; }

        [Column("Longitude")]
        public double Longitude { get; set; }

        [Column("Zone")]
        public string Zone { get; set; } = "";
    }
}