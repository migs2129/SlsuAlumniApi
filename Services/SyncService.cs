using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;

namespace AlumniTrackingAPI.Services
{
    // SyncService — reads new rows from the Alumni Google Sheet tab,
    // saves them to the PendingSubmissions tab, then deletes from Alumni tab.
    public class SyncService
    {
        private readonly GoogleSheetsService _sheets;
        private readonly ILogger<SyncService> _log;

        public SyncService(GoogleSheetsService sheets, ILogger<SyncService> log)
        {
            _sheets = sheets;
            _log = log;
        }

        public async Task<SyncResult> SyncFromSheetAsync()
        {
            _log.LogInformation("[Sync] Starting sync from Google Sheets...");
            var result = new SyncResult();

            List<(int RowIndex, Alumni Alumni)> rows;
            try
            {
                rows = await _sheets.GetAllWithRowIndexAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Sync] Failed to read from Google Sheets.");
                result.Error = "Failed to read from Google Sheets: " + ex.Message;
                return result;
            }

            result.TotalInSheet = rows.Count;
            _log.LogInformation("[Sync] Found {Count} rows in sheet.", rows.Count);

            // Get existing pending fingerprints to avoid duplicates
            var allPending = await _sheets.GetAllPendingAsync();
            var existingSet = allPending
                .Select(p => $"{p.Email}|{p.FullName}|{p.YearGraduated}")
                .ToHashSet();

            var toDelete = new List<(int RowIndex, PendingSubmission Sub)>();

            foreach (var (rowIndex, alumni) in rows)
            {
                var fingerprint = $"{alumni.Email}|{alumni.FullName}|{alumni.YearGraduated}";
                if (existingSet.Contains(fingerprint))
                {
                    result.Skipped++;
                    continue;
                }

                var pending = new PendingSubmission
                {
                    FullName = alumni.FullName,
                    Sex = alumni.Sex,
                    DateOfBirth = alumni.DateOfBirth,
                    PresentAddress = alumni.PresentAddress,
                    ContactNumber = alumni.ContactNumber,
                    Email = alumni.Email ?? alumni.EmailAddress,
                    YearEnrolled = alumni.YearEnrolled,
                    YearGraduated = alumni.YearGraduated,
                    GraduateSchoolProgram = alumni.GraduateSchoolProgram,
                    PassedLicensureExam = alumni.PassedLicensureExam,
                    MonthTaken = alumni.MonthTaken,
                    YearTaken = alumni.YearTaken,
                    PasserStatus = alumni.PasserStatus,
                    Awards = alumni.Awards,
                    JobTitle = alumni.JobTitle,
                    CompanyName = alumni.CompanyName,
                    Industry = alumni.Industry,
                    EmploymentType = alumni.EmploymentType,
                    JobLocation = alumni.JobLocation,
                    Status = "Pending",
                    SubmittedAt = DateTime.UtcNow
                };

                var saved = await _sheets.AddPendingAsync(pending);
                toDelete.Add((rowIndex, saved));
                existingSet.Add(fingerprint);
                result.Imported++;
            }

            // Delete from Alumni tab in reverse order to avoid row shifting
            foreach (var (rowIndex, sub) in toDelete.OrderByDescending(x => x.RowIndex))
            {
                try
                {
                    await _sheets.DeleteRowAsync(rowIndex);
                    result.Deleted++;
                    await Task.Delay(300); // avoid rate limiting
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[Sync] Failed to delete row {RowIndex}", rowIndex);
                    result.DeleteErrors++;
                }
            }

            _log.LogInformation(
                "[Sync] Complete. Imported={I}, Skipped={S}, Deleted={D}, Errors={E}",
                result.Imported, result.Skipped, result.Deleted, result.DeleteErrors);

            return result;
        }
    }

    public class SyncResult
    {
        public int TotalInSheet { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Deleted { get; set; }
        public int DeleteErrors { get; set; }
        public string? Error { get; set; }
    }
}
