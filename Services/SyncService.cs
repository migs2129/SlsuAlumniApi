using AlumniTrackingAPI.Data;
using AlumniTrackingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AlumniTrackingAPI.Services
{
    // ── SyncService ───────────────────────────────────────────────────────
    // Reads new rows from Google Sheets, saves them to SQLite as Pending,
    // then deletes them from the sheet so only verified data lives there.
    //
    // This runs either:
    //   A) On a timer (background service) — automatic polling every N minutes
    //   B) On demand via POST /api/sync — admin triggers it manually
    public class SyncService
    {
        private readonly GoogleSheetsService _sheets;
        private readonly AlumniDbContext _db;
        private readonly ILogger<SyncService> _log;

        public SyncService(
            GoogleSheetsService sheets,
            AlumniDbContext db,
            ILogger<SyncService> log)
        {
            _sheets = sheets;
            _db = db;
            _log = log;
        }

        // ── Main sync method ──────────────────────────────────────────────
        // Returns how many new rows were moved to pending
        public async Task<SyncResult> SyncFromSheetAsync()
        {
            _log.LogInformation("[Sync] Starting sync from Google Sheets...");

            var result = new SyncResult();

            // 1. Get all rows currently in the sheet with their row indexes
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

            _log.LogInformation("[Sync] Found {Count} rows in sheet.", rows.Count);
            result.TotalInSheet = rows.Count;

            // 2. Get emails already in SQLite (approved or pending) to avoid duplicates.
            // We match on Email + FullName + YearGraduated as a composite key
            // since there's no unique ID from Google Forms.
            var existing = await _db.PendingSubmissions
                .Select(p => p.Email + "|" + p.FullName + "|" + p.YearGraduated)
                .ToListAsync();

            var existingSet = existing.ToHashSet();

            // 3. Also check approved alumni already back in the sheet
            // (we don't want to re-import approved records)
            // We'll skip rows that match our "approved" fingerprint

            // 4. Process each row — move to SQLite, delete from sheet
            // IMPORTANT: Delete from highest row index to lowest to avoid
            // row shifting issues when deleting multiple rows
            var toDelete = new List<(int RowIndex, PendingSubmission Sub)>();

            foreach (var (rowIndex, alumni) in rows)
            {
                var fingerprint = $"{alumni.Email}|{alumni.FullName}|{alumni.YearGraduated}";

                if (existingSet.Contains(fingerprint))
                {
                    _log.LogInformation("[Sync] Skipping duplicate: {Name} ({Email})",
                        alumni.FullName, alumni.Email);
                    result.Skipped++;
                    continue;
                }

                // Map Alumni → PendingSubmission
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

                _db.PendingSubmissions.Add(pending);
                toDelete.Add((rowIndex, pending));
                existingSet.Add(fingerprint); // prevent duplicates within this batch
                result.Imported++;
            }

            // 5. Save all pending submissions to SQLite first
            if (toDelete.Count > 0)
            {
                await _db.SaveChangesAsync();
                _log.LogInformation("[Sync] Saved {Count} submissions to SQLite.", toDelete.Count);

                // 6. Delete from sheet in REVERSE order (highest index first)
                // This prevents row shifting from affecting subsequent deletes
                var deleteOrder = toDelete
                    .OrderByDescending(x => x.RowIndex)
                    .ToList();

                foreach (var (rowIndex, sub) in deleteOrder)
                {
                    try
                    {
                        await _sheets.DeleteRowAsync(rowIndex);
                        _log.LogInformation("[Sync] Deleted sheet row {RowIndex} ({Name})",
                            rowIndex, sub.FullName);
                        result.Deleted++;

                        // Small delay between deletions to avoid rate limiting
                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "[Sync] Failed to delete row {RowIndex}", rowIndex);
                        result.DeleteErrors++;
                    }
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
