namespace AlumniTrackingAPI.Models
{
    // Stores board exam results per examination period.
    // Admin can enter these manually OR pull from system data.
    public class ExamResult
    {
        public int Id { get; set; }

        // Exam period
        public string Month { get; set; } = string.Empty; // "April"
        public int Year { get; set; }                 // 2025

        // Data source: "manual" or "system"
        public string DataSource { get; set; } = "Manual";

        // SLSU results
        public int SlsuPassers { get; set; }
        public int SlsuExaminees { get; set; }
        public double SlsuPassingRate { get; set; }

        // First time takers
        public int FirstTimePassers { get; set; }
        public int FirstTimeExaminees { get; set; }
        public double FirstTimePassingRate { get; set; }

        // Repeaters
        public int RepeaterPassers { get; set; }
        public int RepeaterExaminees { get; set; }
        public double RepeaterPassingRate { get; set; }

        // National results
        public int NationalPassers { get; set; }
        public int NationalExaminees { get; set; }
        public double NationalPassingRate { get; set; }

        // Difference from national (can be negative)
        public double DifferenceFromNational { get; set; }

        // Whether to show this result publicly
        public bool IsPublished { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Topnotchers
        public string? TopNotchers { get; set; }
    }
}
