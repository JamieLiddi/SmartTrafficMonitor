using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTrafficMonitor.Models
{
    [Table("sensor_locations", Schema = "public")]
    public class SensorLocation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("sensor_id")]
        public int SensorId { get; set; }

        [Required]
        [Column("sensor_slug")]
        public string SensorSlug { get; set; } = "";

        [Column("latitude")]
        public double? Latitude { get; set; }

        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("zone")]
        public string Zone { get; set; } = "";
    }
}