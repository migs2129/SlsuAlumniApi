using AlumniTrackingAPI.Models;
using System.Text.Json;

namespace AlumniTrackingAPI.Services
{
    public class ExamResultService
    {
        private readonly GoogleSheetsService _sheets;

        public ExamResultService(GoogleSheetsService sheets)
        {
            _sheets = sheets;
        }

        public async Task<List<ExamResult>> GetPublishedAsync()
        {
            var all = await _sheets.GetAllExamResultsAsync();
            return all.Where(e => e.IsPublished)
                .OrderByDescending(e => e.Year)
                .ThenBy(e => MonthOrder(e.Month))
                .ToList();
        }

        public async Task<List<ExamResult>> GetAllAsync()
        {
            var all = await _sheets.GetAllExamResultsAsync();
            return all.OrderByDescending(e => e.Year)
                .ThenBy(e => MonthOrder(e.Month))
                .ToList();
        }

        public async Task<ExamResult?> GetByIdAsync(int id)
            => await _sheets.GetExamResultByIdAsync(id);

        public async Task<ExamResult> CreateAsync(ExamResult result)
            => await _sheets.CreateExamResultAsync(result);

        public async Task<ExamResult?> UpdateAsync(int id, ExamResult updated)
        {
            // Merge top passers: manual (from form) + system (from Sheets Awards field)
            var manualPassers = DeserializeTopPassers(updated.TopNotchers);
            var systemPassers = new List<TopPasser>();

            if (updated.DataSource?.ToLower() == "system")
                systemPassers = await GetSystemTopPassersAsync(updated.Month, updated.Year);

            // Merge, deduplicate by name
            var merged = new List<TopPasser>(systemPassers);
            foreach (var mp in manualPassers)
            {
                if (!merged.Any(s => s.Name.Equals(mp.Name, StringComparison.OrdinalIgnoreCase)))
                    merged.Add(mp);
            }
            merged = merged.OrderBy(p => p.Rank ?? 999).ToList();
            updated.TopNotchers = JsonSerializer.Serialize(merged);

            return await _sheets.UpdateExamResultAsync(id, updated);
        }

        public async Task<bool> TogglePublishedAsync(int id)
            => await _sheets.ToggleExamPublishedAsync(id);

        public async Task<bool> DeleteAsync(int id)
            => await _sheets.DeleteExamResultAsync(id);

        // ── Pull SLSU data from Alumni sheet ──────────────────────────────
        public async Task<ExamResult> PullFromSystemAsync(string month, int year)
        {
            var allAlumni = await _sheets.GetAllAsync();

            var takers = allAlumni
                .Where(a =>
                    a.YearTaken == year.ToString() &&
                    !string.IsNullOrWhiteSpace(a.MonthTaken) &&
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

            // Pull top passers from Awards field
            var systemTopPassers = await GetSystemTopPassersAsync(month, year);

            var result = new ExamResult
            {
                Month = month,
                Year = year,
                DataSource = "System",
                SlsuPassers = slsuPassers,
                SlsuExaminees = slsuTotal,
                FirstTimePassers = firstTime.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                FirstTimeExaminees = firstTime.Count,
                RepeaterPassers = repeaters.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                RepeaterExaminees = repeaters.Count,
                NationalPassers = 0,
                NationalExaminees = 0,
                TopNotchers = JsonSerializer.Serialize(systemTopPassers)
            };

            return result;
        }

        // ── Generate narrative paragraph ───────────────────────────────────
        public static string GenerateNarrative(ExamResult e)
        {
            double slsuRate = e.SlsuExaminees > 0
                ? Math.Round((double)e.SlsuPassers / e.SlsuExaminees * 100, 2) : 0;
            double ftRate = e.FirstTimeExaminees > 0
                ? Math.Round((double)e.FirstTimePassers / e.FirstTimeExaminees * 100, 2) : 0;
            double repRate = e.RepeaterExaminees > 0
                ? Math.Round((double)e.RepeaterPassers / e.RepeaterExaminees * 100, 2) : 0;

            var diff = e.DifferenceFromNational;
            var direction = diff >= 0 ? "higher" : "lower";
            var absDiff = Math.Abs(diff);

            return $"For the {e.Month} {e.Year} Mechanical Engineering Licensure Examination, " +
                   $"SLSU recorded a passing rate of {slsuRate}%, with {e.SlsuPassers} passers " +
                   $"out of {e.SlsuExaminees} examinees. The first-time takers achieved a " +
                   $"passing rate of {ftRate}% ({e.FirstTimePassers} out of {e.FirstTimeExaminees}), " +
                   $"while the repeaters obtained a passing rate of {repRate}% " +
                   $"({e.RepeaterPassers} out of {e.RepeaterExaminees}). " +
                   $"The national passing rate was {e.NationalPassingRate}%, with " +
                   $"{e.NationalPassers:N0} passers out of {e.NationalExaminees:N0} examinees, " +
                   $"indicating that SLSU's performance was {absDiff:F2} percentage points " +
                   $"{direction} than the national average.";
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private async Task<List<TopPasser>> GetSystemTopPassersAsync(string month, int year)
        {
            try
            {
                var all = await _sheets.GetAllAsync();
                return all
                    .Where(a =>
                        a.YearTaken == year.ToString() &&
                        !string.IsNullOrWhiteSpace(a.MonthTaken) &&
                        a.MonthTaken.Equals(month, StringComparison.OrdinalIgnoreCase) &&
                        a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(a.Awards))
                    .Select(a =>
                    {
                        int? rank = null;
                        var numbers = System.Text.RegularExpressions.Regex
                            .Match(a.Awards.ToLower(), @"\d+");
                        if (numbers.Success && int.TryParse(numbers.Value, out var n))
                            rank = n;

                        return new TopPasser
                        {
                            Name = a.FullName,
                            Rank = rank,
                            BatchYear = a.YearGraduated ?? year.ToString(),
                            AwardText = a.Awards
                        };
                    })
                    .OrderBy(p => p.Rank ?? 999)
                    .ToList();
            }
            catch { return new List<TopPasser>(); }
        }

        private static List<TopPasser> DeserializeTopPassers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
                return new List<TopPasser>();
            try { return JsonSerializer.Deserialize<List<TopPasser>>(json) ?? new(); }
            catch { return new List<TopPasser>(); }
        }

        private static int MonthOrder(string month) => month.Trim().ToLower() switch
        {
            "january" or "jan" => 1,
            "february" or "feb" => 2,
            "march" or "mar" => 3,
            "april" or "apr" => 4,
            "may" => 5,
            "june" => 6,
            "july" => 7,
            "august" or "aug" => 8,
            "september" or "sep" => 9,
            "october" or "oct" => 10,
            "november" or "nov" => 11,
            "december" or "dec" => 12,
            _ => 99
        };
    }
}
