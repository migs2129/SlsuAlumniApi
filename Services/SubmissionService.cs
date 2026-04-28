using AlumniTrackingAPI.Data;
using AlumniTrackingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AlumniTrackingAPI.Services
{
    public class SubmissionService
    {
        private readonly AlumniDbContext _db;
        private readonly GoogleSheetsService _sheets;

        public SubmissionService(AlumniDbContext db, GoogleSheetsService sheets)
        {
            _db = db;
            _sheets = sheets;
        }

        public async Task<PendingSubmission> SubmitAsync(PendingSubmission sub)
        {
            sub.Status = "Pending";
            sub.SubmittedAt = DateTime.UtcNow;
            _db.PendingSubmissions.Add(sub);
            await _db.SaveChangesAsync();
            return sub;
        }

        public async Task<List<PendingSubmission>> GetPendingAsync()
            => await _db.PendingSubmissions
                .Where(s => s.Status == "Pending")
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

        public async Task<List<PendingSubmission>> GetAllAsync()
            => await _db.PendingSubmissions
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

        public async Task<(bool ok, string msg)> ApproveAsync(int id, string reviewerUsername)
        {
            var sub = await _db.PendingSubmissions.FindAsync(id);
            if (sub == null) return (false, "Submission not found.");
            if (sub.Status != "Pending") return (false, "Already reviewed.");

            // Map to Alumni using confirmed column order
            var alumni = new Alumni
            {
                Timestamp = DateTime.UtcNow.ToString("M/d/yyyy H:mm:ss"),
                EmailAddress = sub.Email,
                Email = sub.Email,
                Agreement = "I Agree",
                FullName = sub.FullName,
                Sex = sub.Sex,
                DateOfBirth = sub.DateOfBirth,
                PresentAddress = sub.PresentAddress,
                ContactNumber = sub.ContactNumber,
                YearEnrolled = sub.YearEnrolled,
                YearGraduated = sub.YearGraduated,
                GraduateSchoolProgram = sub.GraduateSchoolProgram,
                PassedLicensureExam = sub.PassedLicensureExam,
                MonthTaken = sub.MonthTaken,   // N (13)
                YearTaken = sub.YearTaken,    // O (14)
                PasserStatus = sub.PasserStatus, // P (15)
                Awards = sub.Awards,       // Q (16)
                JobTitle = sub.JobTitle,     // R (17)
                CompanyName = sub.CompanyName,  // S (18)
                Industry = sub.Industry,     // T (19)
                EmploymentType = sub.EmploymentType, // U (20)
                JobLocation = sub.JobLocation,  // V (21)
                PrivacyConsent = "Yes"             // W (22)
            };

            bool pushed = await _sheets.AddAlumniAsync(alumni);
            if (!pushed) return (false, "Failed to write to Google Sheets.");

            sub.Status = "Approved";
            sub.ReviewedAt = DateTime.UtcNow;
            sub.ReviewedBy = reviewerUsername;
            await _db.SaveChangesAsync();
            return (true, "Approved and added to Google Sheets.");
        }

        public async Task<(bool ok, string msg)> RejectAsync(
            int id, string reason, string reviewerUsername)
        {
            var sub = await _db.PendingSubmissions.FindAsync(id);
            if (sub == null) return (false, "Submission not found.");
            if (sub.Status != "Pending") return (false, "Already reviewed.");

            sub.Status = "Rejected";
            sub.RejectionReason = reason;
            sub.ReviewedAt = DateTime.UtcNow;
            sub.ReviewedBy = reviewerUsername;
            await _db.SaveChangesAsync();
            return (true, "Submission rejected.");
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sub = await _db.PendingSubmissions.FindAsync(id);
            if (sub == null) return false;
            _db.PendingSubmissions.Remove(sub);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<int> CountPendingAsync()
            => await _db.PendingSubmissions.CountAsync(s => s.Status == "Pending");
    }
}
