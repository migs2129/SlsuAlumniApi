using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
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

        // ── Confirmed column map (A=0 … W=22) ────────────────────────────
        //  A(0)   Timestamp
        //  B(1)   Email Address
        //  C(2)   Email
        //  D(3)   Agreement
        //  E(4)   Full Name
        //  F(5)   Sex
        //  G(6)   Date of Birth
        //  H(7)   Present Address
        //  I(8)   Contact Number
        //  J(9)   Year Enrolled
        //  K(10)  Year Graduated
        //  L(11)  Graduate School Program
        //  M(12)  Did you pass the RME?
        //  N(13)  Month Taken
        //  O(14)  Year Taken
        //  P(15)  Passer Status  ("First Time Taker" | "Repeater")
        //  Q(16)  Awards
        //  R(17)  Job Title
        //  S(18)  Company Name
        //  T(19)  Industry
        //  U(20)  Employment Type
        //  V(21)  Job Location
        //  W(22)  PRIVACY CONSENT

        private async Task<List<Alumni>> FetchAllAsync()
        {
            var range = $"{_sheetName}!A2:W";
            var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
            var response = await request.ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();

            return rows
                .Where(row => row.Count > 0)            // skip completely empty rows
                .Select(MapRow)
                .Where(a => !string.IsNullOrWhiteSpace(a.FullName)) // skip blank-name rows
                .ToList();
        }


        // ── Public data methods ───────────────────────────────────────────
        public async Task<List<Alumni>> GetAllAsync()
        {
            // Small delay to let Google Sheets commit the last write
            await Task.Delay(1200);
            return await FetchAllAsync();
        }

        public async Task<List<Alumni>> GetByYearGraduatedAsync(string year)
        {
            var all = await FetchAllAsync();
            return all.Where(a => a.YearGraduated == year).ToList();
        }

        public async Task<List<Alumni>> SearchByNameAsync(string keyword)
        {
            var all = await FetchAllAsync();
            return all.Where(a =>
                (a.FullName ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // ── Analytics ─────────────────────────────────────────────────────
        public async Task<object> GetSummaryAsync()
        {
            var all = await FetchAllAsync();
            return new
            {
                totalAlumni = all.Count,
                totalEmployed = all.Count(a => !string.IsNullOrEmpty(a.EmploymentType)),
                totalRmePassers = all.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            };
        }

        public async Task<Dictionary<string, int>> GetGraduatesPerYearAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrEmpty(a.YearGraduated))
                .GroupBy(a => a.YearGraduated)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetEmploymentBreakdownAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrWhiteSpace(a.EmploymentType))
                .GroupBy(a => a.EmploymentType.Trim())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetIndustryBreakdownAsync()
        {
            var all = await FetchAllAsync();
            return all
                .Where(a => !string.IsNullOrWhiteSpace(a.Industry))
                .GroupBy(a => a.Industry.Trim())
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<object> GetRmePassingRateAsync()
        {
            var all = await FetchAllAsync();

            // Only rows that actually answered the RME question
            var takers = all
                .Where(a =>
                    !string.IsNullOrWhiteSpace(a.PassedLicensureExam) &&
                    (a.PassedLicensureExam.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                     a.PassedLicensureExam.Trim().Equals("No", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int total = takers.Count;
            int passed = takers.Count(a =>
                a.PassedLicensureExam.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase));
            int failed = total - passed;
            double rate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

            // Passer status breakdown — guard against null/empty
            var byStatus = takers
                .Where(a =>
                    a.PassedLicensureExam.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(a.PasserStatus))
                .GroupBy(a => a.PasserStatus.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            // By year — guard against null/empty YearTaken
            var byYear = takers
                .Where(a => !string.IsNullOrWhiteSpace(a.YearTaken))
                .GroupBy(a => a.YearTaken.Trim())
                .OrderBy(g => g.Key)
                .Select(yearGroup =>
                {
                    int yTakers = yearGroup.Count();
                    int yPassers = yearGroup.Count(a =>
                        a.PassedLicensureExam.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase));

                    // Guard: avoid divide-by-zero
                    double yRate = yTakers > 0
                        ? Math.Round((double)yPassers / yTakers * 100, 2)
                        : 0;

                    // By month — guard against null/empty MonthTaken
                    var byMonth = yearGroup
                        .Where(a => !string.IsNullOrWhiteSpace(a.MonthTaken))
                        .GroupBy(a => a.MonthTaken.Trim())
                        .OrderBy(g => MonthOrder(g.Key))
                        .Select(mg =>
                        {
                            int mTakers = mg.Count();
                            int mPassers = mg.Count(a =>
                                a.PassedLicensureExam.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase));
                            double mRate = mTakers > 0
                                ? Math.Round((double)mPassers / mTakers * 100, 2)
                                : 0;
                            return new { month = mg.Key, takers = mTakers, passers = mPassers, passingRate = mRate };
                        })
                        .ToList();

                    return new
                    {
                        year = yearGroup.Key,
                        takers = yTakers,
                        passers = yPassers,
                        passingRate = yRate,
                        byMonth
                    };
                })
                .ToList();

            return new
            {
                totalTakers = total,
                totalPassers = passed,
                totalFailed = failed,
                overallPassingRate = rate,
                byPasserStatus = byStatus,
                byYear
            };
        }

        // ── CRUD ──────────────────────────────────────────────────────────
        public async Task<bool> AddAlumniAsync(Alumni alumni)
        {
            // Use just the sheet name as the range — Sheets API will find
            // the first completely empty row after existing data and append there.
            var range = _sheetName;

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { BuildRow(alumni) }
            };

            var req = _sheetsService.Spreadsheets.Values.Append(
                valueRange, _spreadsheetId, range);

            // USER_ENTERED lets Sheets parse dates and numbers naturally
            req.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            // INSERT_ROWS ensures a new row is always inserted, never overwriting
            req.InsertDataOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            var response = await req.ExecuteAsync();

            Console.WriteLine($"[Sheets] Appended to range: {response.Updates?.UpdatedRange} | Rows: {response.Updates?.UpdatedRows}");

            return true;
        }

        public async Task<bool> UpdateAlumniAsync(int rowIndex, Alumni alumni)
        {
            // rowIndex=1 → sheet row 2 (A2), rowIndex=2 → sheet row 3 (A3), etc.
            int sheetRow = rowIndex + 1;
            var range = $"{_sheetName}!A{sheetRow}:W{sheetRow}";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { BuildRow(alumni) }
            };

            var req = _sheetsService.Spreadsheets.Values.Update(
                valueRange, _spreadsheetId, range);
            req.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await req.ExecuteAsync();
            return true;
        }


        public async Task<bool> DeleteAlumniAsync(int rowIndex)
        {
            // Get the spreadsheet metadata to find the correct sheet by name
            var spreadsheet = await _sheetsService.Spreadsheets
                .Get(_spreadsheetId).ExecuteAsync();

            var sheet = spreadsheet.Sheets
                .FirstOrDefault(s => s.Properties.Title == _sheetName);

            if (sheet == null)
            {
                Console.WriteLine($"[Delete] Sheet '{_sheetName}' not found. Available sheets:");
                foreach (var s in spreadsheet.Sheets)
                    Console.WriteLine($"  - '{s.Properties.Title}' (id={s.Properties.SheetId})");
                return false;
            }

            int sheetId = (int)sheet.Properties.SheetId!;
            int startIndex = rowIndex; // 0-based: rowIndex 1 → startIndex 1 = sheet row 2

            Console.WriteLine($"[Delete] Sheet='{_sheetName}' SheetId={sheetId} StartIndex={startIndex} (deleting sheet row {startIndex + 1})");

            var batchReq = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
        {
            new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId    = sheetId,
                        Dimension  = "ROWS",
                        StartIndex = startIndex,
                        EndIndex   = startIndex + 1
                    }
                }
            }
        }
            };

            var response = await _sheetsService.Spreadsheets
                .BatchUpdate(batchReq, _spreadsheetId).ExecuteAsync();

            Console.WriteLine($"[Delete] BatchUpdate complete. Replies: {response.Replies?.Count ?? 0}");
            return true;
        }


        // ── BuildRow — writes in confirmed column order A–W ───────────────
        private static IList<object> BuildRow(Alumni a) => new List<object>
        {
            a.Timestamp,             // A  (0)
            a.EmailAddress,          // B  (1)
            a.Email,                 // C  (2)
            a.Agreement,             // D  (3)
            a.FullName,              // E  (4)
            a.Sex,                   // F  (5)
            a.DateOfBirth,           // G  (6)
            a.PresentAddress,        // H  (7)
            a.ContactNumber,         // I  (8)
            a.YearEnrolled,          // J  (9)
            a.YearGraduated,         // K  (10)
            a.GraduateSchoolProgram, // L  (11)
            a.PassedLicensureExam,   // M  (12)
            a.MonthTaken,            // N  (13)
            a.YearTaken,             // O  (14)
            a.PasserStatus,          // P  (15)
            a.Awards,                // Q  (16)
            a.JobTitle,              // R  (17)
            a.CompanyName,           // S  (18)
            a.Industry,              // T  (19)
            a.EmploymentType,        // U  (20)
            a.JobLocation,           // V  (21)
            a.PrivacyConsent         // W  (22)
        };

        // ── MapRow — reads confirmed column positions ──────────────────────
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
                MonthTaken = Get(13),
                YearTaken = Get(14),
                PasserStatus = Get(15),
                Awards = Get(16),
                JobTitle = Get(17),
                CompanyName = Get(18),
                Industry = Get(19),
                EmploymentType = Get(20),
                JobLocation = Get(21),
                PrivacyConsent = Get(22)
            };
        }

        // Only 4 months used in the Google Form
        private static int MonthOrder(string month) => month.Trim().ToLower() switch
        {
            "february" or "feb" => 2,
            "march" or "mar" => 3,
            "august" or "aug" => 8,
            "september" or "sep" => 9,
            _ => 99
        };
        public async Task<List<(int RowIndex, Alumni Alumni)>> GetAllWithRowIndexAsync()
        {
            var range = $"{_sheetName}!A2:W";
            var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
            var response = await request.ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();

            return rows
                .Select((row, i) => (RowIndex: i + 1, Alumni: MapRow(row)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Alumni.FullName))
                .ToList();
        }

        // Deletes a single row by its 1-based index from the sheet
        // (same as DeleteAlumniAsync but named clearly for the sync flow)
        public async Task<bool> DeleteRowAsync(int rowIndex)
            => await DeleteAlumniAsync(rowIndex);

    }
}
