using System;
using System.ComponentModel.DataAnnotations;

namespace SmartTrafficMonitor.Models
{
    public enum ProjectionMovement
    {
        Pedestrian = 0,
        Cyclist = 1,
        PedestrianPlusCyclist = 2,
        Vehicle = 3
    }

    public class EventImpactScenario
    {
        [Required]
        public string Zone { get; set; } = "All";

        public string? SensorId { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        public DayOfWeek? OverrideDayOfWeek { get; set; }

        public ProjectionMovement Movement { get; set; } = ProjectionMovement.Pedestrian;

        [Range(1, 52)]
        public int LookbackWeeks { get; set; } = 12;

        public bool HasEvent { get; set; }
        [Range(-100, 1000)]
        public double EventUpliftPercent { get; set; } = 0;

        public bool HasVuImpact { get; set; }
        [Range(-100, 1000)]
        public double VuUpliftPercent { get; set; } = 0;

        [Range(0, 23)]
        public int StartHour { get; set; } = 12;

        [Range(1, 24)]
        public int DurationHours { get; set; } = 6;

        [Range(0, 200)]
        public double UncertaintyPercent { get; set; } = 15;
    }
}