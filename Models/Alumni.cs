namespace AlumniTrackingAPI.Models
{
    public class Alumni
    {
        public string Timestamp { get; set; } = string.Empty; // A  (0)
        public string EmailAddress { get; set; } = string.Empty; // B  (1)
        public string Email { get; set; } = string.Empty; // C  (2)
        public string Agreement { get; set; } = string.Empty; // D  (3)
        public string FullName { get; set; } = string.Empty; // E  (4)
        public string Sex { get; set; } = string.Empty; // F  (5)
        public string DateOfBirth { get; set; } = string.Empty; // G  (6)
        public string PresentAddress { get; set; } = string.Empty; // H  (7)
        public string ContactNumber { get; set; } = string.Empty; // I  (8)
        public string YearEnrolled { get; set; } = string.Empty; // J  (9)
        public string YearGraduated { get; set; } = string.Empty; // K  (10)
        public string GraduateSchoolProgram { get; set; } = string.Empty; // L  (11)
        public string PassedLicensureExam { get; set; } = string.Empty; // M  (12)
        public string MonthTaken { get; set; } = string.Empty; // N  (13)
        public string YearTaken { get; set; } = string.Empty; // O  (14)
        public string PasserStatus { get; set; } = string.Empty; // P  (15) — "First Time Taker" | "Repeater"
        public string Awards { get; set; } = string.Empty; // Q  (16)
        public string JobTitle { get; set; } = string.Empty; // R  (17)
        public string CompanyName { get; set; } = string.Empty; // S  (18)
        public string Industry { get; set; } = string.Empty; // T  (19)
        public string EmploymentType { get; set; } = string.Empty; // U  (20)
        public string JobLocation { get; set; } = string.Empty; // V  (21)
        public string PrivacyConsent { get; set; } = string.Empty; // W  (22)
    }
}
