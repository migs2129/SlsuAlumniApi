using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using AlumniTrackingAPI.Models;

namespace AlumniTrackingAPI.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService _sheetsService;
        private readonly string _spreadsheetId;
        private readonly string _sheetName;

        public GoogleSheetsService(IConfiguration config)
        {
            var credPath = config["GoogleSheets:CredentialsPath"]!;
            _spreadsheetId = config["GoogleSheets:SpreadsheetId"]!;
            _sheetName = config["GoogleSheets:SheetName"]!;

            GoogleCredential credential;
            using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential
                    .FromStream(stream)
                    .CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            _sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "AlumniTrackingSystem"
            });
        }

        // ── Fetch all rows from sheet ──────────────────────────────────────────
        private async Task<List<Alumni>> FetchAllAsync()
        {
            var range = $"{_sheetName}!A2:W";
            var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
            var response = await request.ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            return rows.Select(MapRow).ToList();
        }

        // ── Public: Get all alumni ─────────────────────────────────────────────
        public async Task<List<Alumni>> GetAllAsync() => await FetchAllAsync();

        // ── Public: Get by graduation year ────────────────────────────────────
        public async Task<List<Alumni>> GetByYearGraduatedAsync(string year)
        {
            var all = await FetchAllAsync();
            return all.Where(a => a.YearGraduated == year).ToList();
        }

        // ── Public: Get by employment type ────────────────────────────────────
        public async Task<List<Alumni>> GetByEmploymentTypeAsync(string type)
        {
            var all = await FetchAllAsync();
            return all.Where(a =>
                a.EmploymentType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ── Public: Get by industry ───────────────────────────────────────────
        public async Task<List<Alumni>> GetByIndustryAsync(string industry)
        {
            var all = await FetchAllAsync();
            return all.Where(a =>
                a.Industry.Contains(industry, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ── Public: Search by name ────────────────────────────────────────────
        public async Task<List<Alumni>> SearchByNameAsync(string keyword)
        {
            var all = await FetchAllAsync();
            return all.Where(a =>
                a.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ANALYTICS — for bar graphs
        // ═══════════════════════════════════════════════════════════════════════

        // ── Graduates per year ────────────────────────────────────────────────
        public async Task<Dictionary<string, int>> GetGraduatesPerYearAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrEmpty(a.YearGraduated))
                .GroupBy(a => a.YearGraduated)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // ── Employment type breakdown ─────────────────────────────────────────
        public async Task<Dictionary<string, int>> GetEmploymentBreakdownAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrEmpty(a.EmploymentType))
                .GroupBy(a => a.EmploymentType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // ── Industry breakdown ────────────────────────────────────────────────
        public async Task<Dictionary<string, int>> GetIndustryBreakdownAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrEmpty(a.Industry))
                .GroupBy(a => a.Industry)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // ── Sex breakdown ─────────────────────────────────────────────────────
        public async Task<Dictionary<string, int>> GetSexBreakdownAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrEmpty(a.Sex))
                .GroupBy(a => a.Sex)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // ── RME Passing Rate ──────────────────────────────────────────────────
        public async Task<object> GetRmePassingRateAsync()
        {
            var all = await FetchAllAsync();

            // Only include alumni who took the exam (answered Yes or No)
            var takers = all
                .Where(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                         || a.PassedLicensureExam.Equals("No", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = takers.Count;
            int passed = takers.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase));
            int failed = total - passed;
            double rate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

            // Breakdown by passer status (First Time, Second Time, etc.)
            var byStatus = takers
                .Where(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(a.PasserStatus))
                .GroupBy(a => a.PasserStatus)
                .ToDictionary(g => g.Key, g => g.Count());

            // Passing rate per year taken
            var byYear = takers
                .Where(a => !string.IsNullOrEmpty(a.YearTaken))
                .GroupBy(a => a.YearTaken)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    year = g.Key,
                    takers = g.Count(),
                    passers = g.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                    passingRate = Math.Round(
                        (double)g.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                        / g.Count() * 100, 2)
                })
                .ToList();

            return new
            {
                totalTakers = total,
                totalPassers = passed,
                totalFailed = failed,
                overallPassingRate = rate,
                byPasserStatus = byStatus,
                byYear = byYear
            };
        }

        // ── Summary (dashboard numbers) ───────────────────────────────────────
        public async Task<object> GetSummaryAsync()
        {
            var all = await FetchAllAsync();
            return new
            {
                totalAlumni = all.Count,
                totalEmployed = all.Count(a => !string.IsNullOrEmpty(a.EmploymentType)),
                totalRmeTakers = all.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                    a.PassedLicensureExam.Equals("No", StringComparison.OrdinalIgnoreCase)),
                totalRmePassers = all.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ROW MAPPER
        // ═══════════════════════════════════════════════════════════════════════
        private static Alumni MapRow(IList<object> row)
        {
            string Get(int i) => row.Count > i ? row[i]?.ToString() ?? "" : "";

            return new Alumni
            {
                Timestamp = Get(0),
                EmailAddress = Get(1),
                Email = Get(2),
                Agreement = Get(3),
                FullName = Get(4),
                Sex = Get(5),
                DateOfBirth = Get(6),
                PresentAddress = Get(7),
                ContactNumber = Get(8),
                YearEnrolled = Get(9),
                YearGraduated = Get(10),
                GraduateSchoolProgram = Get(11),
                PassedLicensureExam = Get(12),
                PasserStatus = Get(13),
                Awards = Get(14),
                YearTaken = Get(15),
                JobTitle = Get(16),
                CompanyName = Get(17),
                Industry = Get(18),
                EmploymentType = Get(19),
                JobLocation = Get(20),
                PrivacyConsent = Get(21)
            };
        }
    }
}