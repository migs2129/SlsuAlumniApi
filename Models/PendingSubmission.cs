namespace AlumniTrackingAPI.Models
{
    public class PendingSubmission
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string PresentAddress { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string YearEnrolled { get; set; } = string.Empty;
        public string YearGraduated { get; set; } = string.Empty;
        public string GraduateSchoolProgram { get; set; } = string.Empty;
        public string PassedLicensureExam { get; set; } = string.Empty;
        public string MonthTaken { get; set; } = string.Empty;
        public string YearTaken { get; set; } = string.Empty;
        public string PasserStatus { get; set; } = string.Empty; // "First Time Taker" | "Repeater"
        public string Awards { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string EmploymentType { get; set; } = string.Empty;
        public string JobLocation { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";   // Pending | Approved | Rejected
        public string? RejectionReason { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
    }
}
