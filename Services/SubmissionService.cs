using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;

namespace AlumniTrackingAPI.Services
{
    public class SubmissionService
    {
        private readonly GoogleSheetsService _sheets;

        public SubmissionService(GoogleSheetsService sheets)
        {
            _sheets = sheets;
        }

        public async Task<PendingSubmission> SubmitAsync(PendingSubmission sub)
            => await _sheets.AddPendingAsync(sub);

        public async Task<List<PendingSubmission>> GetPendingAsync()
        {
            var all = await _sheets.GetAllPendingAsync();
            return all.Where(s => s.Status == "Pending")
                      .OrderByDescending(s => s.SubmittedAt)
                      .ToList();
        }

        public async Task<List<PendingSubmission>> GetAllAsync()
        {
            var all = await _sheets.GetAllPendingAsync();
            return all.OrderByDescending(s => s.SubmittedAt).ToList();
        }

        public async Task<int> CountPendingAsync()
        {
            var all = await _sheets.GetAllPendingAsync();
            return all.Count(s => s.Status == "Pending");
        }

        public async Task<(bool ok, string msg)> ApproveAsync(int id, string reviewerUsername)
        {
            var all = await _sheets.GetAllPendingAsync();
            var sub = all.FirstOrDefault(s => s.Id == id);
            if (sub == null) return (false, "Submission not found.");
            if (sub.Status != "Pending") return (false, "Already reviewed.");

            // Write to Alumni tab
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
                MonthTaken = sub.MonthTaken,
                YearTaken = sub.YearTaken,
                PasserStatus = sub.PasserStatus,
                Awards = sub.Awards,
                JobTitle = sub.JobTitle,
                CompanyName = sub.CompanyName,
                Industry = sub.Industry,
                EmploymentType = sub.EmploymentType,
                JobLocation = sub.JobLocation,
                PrivacyConsent = "Yes"
            };

            bool pushed = await _sheets.AddAlumniAsync(alumni);
            if (!pushed) return (false, "Failed to write to Google Sheets.");

            await _sheets.UpdatePendingStatusAsync(id, "Approved", reviewerUsername);
            return (true, "Approved and added to Google Sheets.");
        }

        public async Task<(bool ok, string msg)> RejectAsync(
            int id, string reason, string reviewerUsername)
        {
            var all = await _sheets.GetAllPendingAsync();
            var sub = all.FirstOrDefault(s => s.Id == id);
            if (sub == null) return (false, "Submission not found.");
            if (sub.Status != "Pending") return (false, "Already reviewed.");

            await _sheets.UpdatePendingStatusAsync(id, "Rejected", reviewerUsername, reason);
            return (true, "Submission rejected.");
        }

        public async Task<bool> DeleteAsync(int id)
            => await _sheets.DeletePendingAsync(id);
    }
}
