using System;

namespace AlumniTrackingAPI.Models
{
    public class Alumni
    {
        public string Timestamp { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Agreement { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string PresentAddress { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string YearEnrolled { get; set; } = string.Empty;
        public string YearGraduated { get; set; } = string.Empty;
        public string GraduateSchoolProgram { get; set; } = string.Empty;
        public string PassedLicensureExam { get; set; } = string.Empty; // Yes/No
        public string PasserStatus { get; set; } = string.Empty; // First Time, etc.
        public string Awards { get; set; } = string.Empty;
        public string YearTaken { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string EmploymentType { get; set; } = string.Empty;
        public string JobLocation { get; set; } = string.Empty;
        public string PrivacyConsent { get; set; } = string.Empty;
    }
}
