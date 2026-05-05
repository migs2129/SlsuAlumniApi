using AlumniTrackingAPI.Data;
using AlumniTrackingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AlumniTrackingAPI.Services
{
    public class ExamResultService
    {
        private readonly AlumniDbContext _db;
        private readonly GoogleSheetsService _sheets;

        public ExamResultService(AlumniDbContext db, GoogleSheetsService sheets)
        {
            _db = db;
            _sheets = sheets;
        }

        // ── Public: published results only ───────────────────────────────
        public async Task<List<ExamResult>> GetPublishedAsync()
        {
            var data = await _db.ExamResults
                .Where(e => e.IsPublished)
                .ToListAsync(); // ✅ FETCH FIRST

            return data
                .OrderByDescending(e => e.Year)
                .ThenBy(e => MonthOrder(e.Month)) // ✅ SAFE NOW
                .ToList();
        }

        // ── Admin: all results ────────────────────────────────────────────
        public async Task<List<ExamResult>> GetAllAsync()
        {
            var data = await _db.ExamResults.ToListAsync();

            return data
                .OrderByDescending(e => e.Year)
                .ThenBy(e => MonthOrder(e.Month ?? ""))
                .ToList();
        }

        public async Task<ExamResult?> GetByIdAsync(int id)
            => await _db.ExamResults.FindAsync(id);

        // ── Create ────────────────────────────────────────────────────────
        public async Task<ExamResult> CreateAsync(ExamResult result)
        {
            result = ComputeFields(result);
            result.CreatedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;

            _db.ExamResults.Add(result);
            await _db.SaveChangesAsync();
            return result;
        }

        // ── Update ────────────────────────────────────────────────────────
        public async Task<ExamResult?> UpdateAsync(int id, ExamResult updated)
        {
            var existing = await _db.ExamResults.FindAsync(id);
            if (existing == null) return null;

            existing.Month = updated.Month;
            existing.Year = updated.Year;
            existing.DataSource = updated.DataSource;
            existing.SlsuPassers = updated.SlsuPassers;
            existing.SlsuExaminees = updated.SlsuExaminees;
            existing.FirstTimePassers = updated.FirstTimePassers;
            existing.FirstTimeExaminees = updated.FirstTimeExaminees;
            existing.RepeaterPassers = updated.RepeaterPassers;
            existing.RepeaterExaminees = updated.RepeaterExaminees;
            existing.NationalPassers = updated.NationalPassers;
            existing.NationalExaminees = updated.NationalExaminees;
            existing.IsPublished = updated.IsPublished;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.TopNotchers = updated.TopNotchers;

            existing = ComputeFields(existing);

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> TogglePublishedAsync(int id)
        {
            var result = await _db.ExamResults.FindAsync(id);
            if (result == null) return false;

            result.IsPublished = !result.IsPublished;
            result.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }
        public static string GenerateNarrative(ExamResult e)
        {
            var diff = e.DifferenceFromNational;
            var direction = diff >= 0 ? "higher" : "lower";
            var abs = Math.Abs(diff);

            return $"For {e.Month} {e.Year} Mechanical Engineering Licensure Examination, SLSU recorded a passing rate of {e.SlsuPassingRate}%, which consists of {e.SlsuPassers} Passers and a total of {e.SlsuExaminees} examinees. " +
                    $"The first time takers passing rate is {(e.FirstTimeExaminees == 0 ? 0 :
 Math.Round((double)e.FirstTimePassers / e.FirstTimeExaminees * 100, 2))}%, which consists of {e.FirstTimePassers} passers and a total of {e.FirstTimeExaminees} examinees." +
                    $"The repeaters passing rate is {(e.RepeaterExaminees == 0 ? 0 :
 Math.Round((double)e.RepeaterPassers / e.RepeaterExaminees * 100, 2))} %.\n"+
                   $"The {e.Month} {e.Year} Mechanical Engineering Licensure Examination is {abs:F2}% {direction} than the national average of {e.NationalPassingRate}% which consists of {e.NationalPassers} passers over {e.NationalExaminees} examinees).";
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var result = await _db.ExamResults.FindAsync(id);
            if (result == null) return false;

            _db.ExamResults.Remove(result);
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Pull SLSU data ────────────────────────────────────────────────
        public async Task<ExamResult> PullFromSystemAsync(string month, int year)
        {
            var allAlumni = await _sheets.GetAllAsync();

            var takers = allAlumni
                .Where(a =>
                    a.YearTaken == year.ToString() &&
                    a.MonthTaken.Equals(month, StringComparison.OrdinalIgnoreCase) &&
                   (a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                    a.PassedLicensureExam.Equals("No", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int slsuTotal = takers.Count;
            int slsuPassers = takers.Count(a =>
                a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase));

            var firstTime = takers
                .Where(a => a.PasserStatus.Equals("First Time Taker", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var repeaters = takers
                .Where(a => a.PasserStatus.Equals("Repeater", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return ComputeFields(new ExamResult
            {
                Month = month,
                Year = year,
                DataSource = "System",
                SlsuPassers = slsuPassers,
                SlsuExaminees = slsuTotal,
                FirstTimePassers = firstTime.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                FirstTimeExaminees = firstTime.Count,
                RepeaterPassers = repeaters.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                RepeaterExaminees = repeaters.Count,
                NationalPassers = 0,
                NationalExaminees = 0
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static ExamResult ComputeFields(ExamResult e)
        {
            e.SlsuPassingRate = e.SlsuExaminees > 0
                ? Math.Round((double)e.SlsuPassers / e.SlsuExaminees * 100, 2) : 0;

            e.FirstTimePassingRate = e.FirstTimeExaminees > 0
                ? Math.Round((double)e.FirstTimePassers / e.FirstTimeExaminees * 100, 2) : 0;

            e.RepeaterPassingRate = e.RepeaterExaminees > 0
                ? Math.Round((double)e.RepeaterPassers / e.RepeaterExaminees * 100, 2) : 0;

            e.NationalPassingRate = e.NationalExaminees > 0
                ? Math.Round((double)e.NationalPassers / e.NationalExaminees * 100, 2) : 0;

            e.DifferenceFromNational =
                Math.Round(e.SlsuPassingRate - e.NationalPassingRate, 2);

            return e;
        }

        // ✅ keep this — now safe
        private static int MonthOrder(string month)
        {
            if (string.IsNullOrWhiteSpace(month)) return 99;

            return month.Trim().ToLower() switch
            {
                "january" or "jan" => 1,
                "february" or "feb" => 2,
                "march" or "mar" => 3,
                "april" or "apr" => 4,
                "may" => 5,
                "june" => 6,
                "july" => 7,
                "august" or "aug" => 8,
                "september" or "sep" or "sept" => 9,
                "october" or "oct" => 10,
                "november" or "nov" => 11,
                "december" or "dec" => 12,
                _ => 99
            };
        }
    }
}