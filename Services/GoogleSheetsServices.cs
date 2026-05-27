// ═══════════════════════════════════════════════════════════════
//  GoogleSheetsService.cs — FULL REPLACEMENT
//  All data (Alumni, ExamResults, PendingSubmissions, AdminUsers)
//  now lives in Google Sheets tabs. No SQLite needed.
//
//  SHEET TABS REQUIRED (create these in your Google Sheet):
//    1. Alumni             ← already exists
//    2. ExamResults        ← new
//    3. PendingSubmissions ← new
//    4. AdminUsers         ← new
// ═══════════════════════════════════════════════════════════════

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using AlumniTrackingAPI.Models;
 

namespace AlumniTrackingAPI.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService _service;
        private readonly string _spreadsheetId;
        private readonly string _alumniSheet;

        // Tab names — must exactly match your Google Sheet tab names
        private const string TAB_ALUMNI = "Alumni";
        private const string TAB_EXAM = "ExamResults";
        private const string TAB_PENDING = "PendingSubmissions";
        private const string TAB_ADMIN = "AdminUsers";

        public GoogleSheetsService(IConfiguration config)
        {
            
            _spreadsheetId = config["GoogleSheets:SpreadsheetId"] ?? throw new Exception("SpreadsheetId missing");
            _alumniSheet = config["GoogleSheets:SheetName"] ?? TAB_ALUMNI;

            var credential = GoogleCredential
                .FromJson($@"
                    {{
                      ""type"": ""service_account"",
                      ""project_id"": ""{Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID")}"" ,
                      ""private_key"": ""{Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_KEY")}"",
                      ""client_email"": ""{Environment.GetEnvironmentVariable("GOOGLE_CLIENT_EMAIL")}"",
                      ""token_uri"": ""https://oauth2.googleapis.com/token""
                    }}");
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            _service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "SLSU Alumni Tracking"
            });
        }

        // ═══════════════════════════════════════════════════════
        //  ALUMNI (existing tab)
        // ═══════════════════════════════════════════════════════

        public async Task<List<Alumni>> GetAllAsync()
            => await FetchAlumniAsync();

        private async Task<List<Alumni>> FetchAlumniAsync()
        {
            var range = $"{_alumniSheet}!A2:W";
            var request = _service.Spreadsheets.Values.Get(_spreadsheetId, range);
            var response = await request.ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();

            return rows
                .Where(r => r.Count > 0)
                .Select(MapAlumniRow)
                .Where(a => !string.IsNullOrWhiteSpace(a.FullName))
                .ToList();
        }

        private static Alumni MapAlumniRow(IList<object> r)
        {
            string Get(int i) => i < r.Count ? r[i]?.ToString()?.Trim() ?? "" : "";
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

        private static IList<object> AlumniToRow(Alumni a) => new List<object>
        {
            a.Timestamp, a.EmailAddress, a.Email, a.Agreement,
            a.FullName, a.Sex, a.DateOfBirth, a.PresentAddress, a.ContactNumber,
            a.YearEnrolled, a.YearGraduated, a.GraduateSchoolProgram,
            a.PassedLicensureExam, a.MonthTaken, a.YearTaken, a.PasserStatus, a.Awards,
            a.JobTitle, a.CompanyName, a.Industry, a.EmploymentType, a.JobLocation,
            a.PrivacyConsent
        };

        public async Task<bool> AddAlumniAsync(Alumni alumni)
        {
            var vr = new ValueRange { Values = new List<IList<object>> { AlumniToRow(alumni) } };
            var req = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, _alumniSheet);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            req.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await req.ExecuteAsync();
            return true;
        }

        public async Task<bool> UpdateAlumniAsync(int rowIndex, Alumni alumni)
        {
            int sheetRow = rowIndex + 1;
            var range = $"{_alumniSheet}!A{sheetRow}:W{sheetRow}";
            var vr = new ValueRange { Values = new List<IList<object>> { AlumniToRow(alumni) } };
            var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req.ExecuteAsync();
            return true;
        }

        public async Task<bool> DeleteAlumniAsync(int rowIndex)
            => await DeleteRowFromTab(_alumniSheet, rowIndex);

        public async Task<List<(int RowIndex, Alumni Alumni)>> GetAllWithRowIndexAsync()
        {
            var range = $"{_alumniSheet}!A2:W";
            var request = _service.Spreadsheets.Values.Get(_spreadsheetId, range);
            var response = await request.ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();

            return rows
                .Select((row, i) => (RowIndex: i + 1, Alumni: MapAlumniRow(row)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Alumni.FullName))
                .ToList();
        }

        public async Task<bool> DeleteRowAsync(int rowIndex)
            => await DeleteRowFromTab(_alumniSheet, rowIndex);

        // ── Analytics ────────────────────────────────────────────────────
        public async Task<object> GetSummaryAsync()
        {
            var all = await FetchAlumniAsync();
            return new
            {
                totalAlumni = all.Count,
                totalEmployed = all.Count(a => !string.IsNullOrWhiteSpace(a.EmploymentType)),
                totalRmePassers = all.Count(a =>
                    a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            };
        }

        public async Task<Dictionary<string, int>> GetGraduatesPerYearAsync()
        {
            var all = await FetchAlumniAsync();
            return all
                .Where(a => !string.IsNullOrWhiteSpace(a.YearGraduated))
                .GroupBy(a => a.YearGraduated.Trim())
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetEmploymentBreakdownAsync()
        {
            var all = await FetchAlumniAsync();
            return all
                .Where(a => !string.IsNullOrWhiteSpace(a.EmploymentType))
                .GroupBy(a => a.EmploymentType.Trim())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetIndustryBreakdownAsync()
        {
            var all = await FetchAlumniAsync();
            return all
                .Where(a => !string.IsNullOrWhiteSpace(a.Industry))
                .GroupBy(a => a.Industry.Trim())
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<object> GetRmePassingRateAsync()
        {
            var all = await FetchAlumniAsync();
            var takers = all.Where(a =>
                a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                a.PassedLicensureExam.Equals("No", StringComparison.OrdinalIgnoreCase)).ToList();

            int total = takers.Count;
            int passed = takers.Count(a =>
                a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase));
            double rate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

            var byYear = takers
                .Where(a => !string.IsNullOrWhiteSpace(a.YearTaken))
                .GroupBy(a => a.YearTaken.Trim())
                .OrderBy(g => g.Key)
                .Select(yg =>
                {
                    int yT = yg.Count();
                    int yP = yg.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase));
                    return new
                    {
                        year = yg.Key,
                        takers = yT,
                        passers = yP,
                        passingRate = yT > 0 ? Math.Round((double)yP / yT * 100, 2) : 0,
                        byMonth = yg.Where(a => !string.IsNullOrWhiteSpace(a.MonthTaken))
                            .GroupBy(a => a.MonthTaken.Trim())
                            .Select(mg =>
                            {
                                int mT = mg.Count();
                                int mP = mg.Count(a => a.PassedLicensureExam.Equals("Yes", StringComparison.OrdinalIgnoreCase));
                                return new { month = mg.Key, takers = mT, passers = mP, passingRate = mT > 0 ? Math.Round((double)mP / mT * 100, 2) : 0 };
                            }).ToList()
                    };
                }).ToList();

            return new { totalTakers = total, totalPassers = passed, overallPassingRate = rate, byYear };
        }

        public async Task<List<Alumni>> GetByYearGraduatedAsync(string year)
        {
            var all = await FetchAlumniAsync();
            return all.Where(a => a.YearGraduated == year).ToList();
        }

        public async Task<List<Alumni>> SearchByNameAsync(string keyword)
        {
            var all = await FetchAlumniAsync();
            return all.Where(a =>
                (a.FullName ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ═══════════════════════════════════════════════════════
        //  EXAM RESULTS (new tab: ExamResults)
        //
        //  Columns A-Z+ stored as:
        //  A=Id, B=Month, C=Year, D=DataSource,
        //  E=SlsuPassers, F=SlsuExaminees, G=SlsuPassingRate,
        //  H=FirstTimePassers, I=FirstTimeExaminees, J=FirstTimePassingRate,
        //  K=RepeaterPassers, L=RepeaterExaminees, M=RepeaterPassingRate,
        //  N=NationalPassers, O=NationalExaminees, P=NationalPassingRate,
        //  Q=DifferenceFromNational, R=IsPublished,
        //  S=TopNotchers(JSON), T=CreatedAt, U=UpdatedAt
        // ═══════════════════════════════════════════════════════

        public async Task<List<ExamResult>> GetAllExamResultsAsync()
        {
            var range = $"{TAB_EXAM}!A2:U";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            return rows.Where(r => r.Count > 0).Select(MapExamRow).ToList();
        }

        public async Task<ExamResult?> GetExamResultByIdAsync(int id)
        {
            var all = await GetAllExamResultsAsync();
            return all.FirstOrDefault(e => e.Id == id);
        }

        public async Task<ExamResult> CreateExamResultAsync(ExamResult e)
        {
            // Auto-generate ID (max existing + 1)
            var all = await GetAllExamResultsAsync();
            e.Id = all.Count > 0 ? all.Max(x => x.Id) + 1 : 1;
            e.CreatedAt = DateTime.UtcNow;
            e.UpdatedAt = DateTime.UtcNow;
            e = ComputeExamFields(e);

            var vr = new ValueRange { Values = new List<IList<object>> { ExamToRow(e) } };
            var req = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, TAB_EXAM);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            req.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await req.ExecuteAsync();
            return e;
        }

        public async Task<ExamResult?> UpdateExamResultAsync(int id, ExamResult updated)
        {
            var (rowIndex, existing) = await FindExamRowIndex(id);
            if (existing == null) return null;

            updated.Id = id;
            updated.CreatedAt = existing.CreatedAt;
            updated.UpdatedAt = DateTime.UtcNow;
            updated = ComputeExamFields(updated);

            int sheetRow = rowIndex + 1;
            var range = $"{TAB_EXAM}!A{sheetRow}:U{sheetRow}";
            var vr = new ValueRange { Values = new List<IList<object>> { ExamToRow(updated) } };
            var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req.ExecuteAsync();
            return updated;
        }

        public async Task<bool> ToggleExamPublishedAsync(int id)
        {
            var (rowIndex, existing) = await FindExamRowIndex(id);
            if (existing == null) return false;

            existing.IsPublished = !existing.IsPublished;
            existing.UpdatedAt = DateTime.UtcNow;

            int sheetRow = rowIndex + 1;
            var range = $"{TAB_EXAM}!A{sheetRow}:U{sheetRow}";
            var vr = new ValueRange { Values = new List<IList<object>> { ExamToRow(existing) } };
            var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req.ExecuteAsync();
            return true;
        }

        public async Task<bool> DeleteExamResultAsync(int id)
        {
            var (rowIndex, existing) = await FindExamRowIndex(id);
            if (existing == null) return false;
            return await DeleteRowFromTab(TAB_EXAM, rowIndex);
        }

        private async Task<(int RowIndex, ExamResult? Result)> FindExamRowIndex(int id)
        {
            var range = $"{TAB_EXAM}!A2:U";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count > 0 && int.TryParse(r[0]?.ToString(), out var rowId) && rowId == id)
                    return (i + 1, MapExamRow(r));
            }
            return (0, null);
        }

        private static ExamResult MapExamRow(IList<object> r)
        {
            string Get(int i) => i < r.Count ? r[i]?.ToString()?.Trim() ?? "" : "";
            int GetInt(int i) => int.TryParse(Get(i), out var v) ? v : 0;
            double GetDbl(int i) => double.TryParse(Get(i), out var v) ? v : 0;
            bool GetBool(int i) => Get(i).Equals("true", StringComparison.OrdinalIgnoreCase);

            return new ExamResult
            {
                Id = GetInt(0),
                Month = Get(1),
                Year = GetInt(2),
                DataSource = Get(3),
                SlsuPassers = GetInt(4),
                SlsuExaminees = GetInt(5),
                SlsuPassingRate = GetDbl(6),
                FirstTimePassers = GetInt(7),
                FirstTimeExaminees = GetInt(8),
                FirstTimePassingRate = GetDbl(9),
                RepeaterPassers = GetInt(10),
                RepeaterExaminees = GetInt(11),
                RepeaterPassingRate = GetDbl(12),
                NationalPassers = GetInt(13),
                NationalExaminees = GetInt(14),
                NationalPassingRate = GetDbl(15),
                DifferenceFromNational = GetDbl(16),
                IsPublished = GetBool(17),
                TopNotchers = Get(18),   // stored as JSON string
                CreatedAt = DateTime.TryParse(Get(19), out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(Get(20), out var ua) ? ua : DateTime.UtcNow
            };
        }

        private static IList<object> ExamToRow(ExamResult e) => new List<object>
        {
            e.Id, e.Month, e.Year, e.DataSource,
            e.SlsuPassers, e.SlsuExaminees, e.SlsuPassingRate,
            e.FirstTimePassers, e.FirstTimeExaminees, e.FirstTimePassingRate,
            e.RepeaterPassers, e.RepeaterExaminees, e.RepeaterPassingRate,
            e.NationalPassers, e.NationalExaminees, e.NationalPassingRate,
            e.DifferenceFromNational,
            e.IsPublished.ToString().ToLower(),
            e.TopNotchers ?? "[]",
            e.CreatedAt.ToString("O"),
            e.UpdatedAt.ToString("O")
        };

        private static ExamResult ComputeExamFields(ExamResult e)
        {
            e.SlsuPassingRate = e.SlsuExaminees > 0 ? Math.Round((double)e.SlsuPassers / e.SlsuExaminees * 100, 2) : 0;
            e.FirstTimePassingRate = e.FirstTimeExaminees > 0 ? Math.Round((double)e.FirstTimePassers / e.FirstTimeExaminees * 100, 2) : 0;
            e.RepeaterPassingRate = e.RepeaterExaminees > 0 ? Math.Round((double)e.RepeaterPassers / e.RepeaterExaminees * 100, 2) : 0;
            e.NationalPassingRate = e.NationalExaminees > 0 ? Math.Round((double)e.NationalPassers / e.NationalExaminees * 100, 2) : 0;
            e.DifferenceFromNational = Math.Round(e.SlsuPassingRate - e.NationalPassingRate, 2);
            return e;
        }

        // ═══════════════════════════════════════════════════════
        //  PENDING SUBMISSIONS (new tab: PendingSubmissions)
        //
        //  Columns: A=Id, B=FullName, C=Sex, D=DateOfBirth,
        //  E=PresentAddress, F=ContactNumber, G=Email,
        //  H=YearEnrolled, I=YearGraduated, J=GraduateSchoolProgram,
        //  K=PassedLicensureExam, L=MonthTaken, M=YearTaken,
        //  N=PasserStatus, O=Awards, P=JobTitle, Q=CompanyName,
        //  R=Industry, S=EmploymentType, T=JobLocation,
        //  U=Status, V=RejectionReason, W=SubmittedAt,
        //  X=ReviewedAt, Y=ReviewedBy
        // ═══════════════════════════════════════════════════════

        public async Task<List<PendingSubmission>> GetAllPendingAsync()
        {
            var range = $"{TAB_PENDING}!A2:Y";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            return rows.Where(r => r.Count > 0).Select(MapPendingRow)
                .OrderByDescending(s => s.SubmittedAt).ToList();
        }

        public async Task<PendingSubmission> AddPendingAsync(PendingSubmission sub)
        {
            var all = await GetAllPendingAsync();
            sub.Id = all.Count > 0 ? all.Max(s => s.Id) + 1 : 1;
            sub.SubmittedAt = DateTime.UtcNow;
            sub.Status = "Pending";

            var vr = new ValueRange { Values = new List<IList<object>> { PendingToRow(sub) } };
            var req = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, TAB_PENDING);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            req.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await req.ExecuteAsync();
            return sub;
        }

        public async Task<bool> UpdatePendingStatusAsync(int id, string status,
            string? reviewedBy = null, string? rejectionReason = null)
        {
            var (rowIndex, sub) = await FindPendingRowIndex(id);
            if (sub == null) return false;

            sub.Status = status;
            sub.ReviewedBy = reviewedBy;
            sub.RejectionReason = rejectionReason;
            sub.ReviewedAt = DateTime.UtcNow;

            int sheetRow = rowIndex + 1;
            var range = $"{TAB_PENDING}!A{sheetRow}:Y{sheetRow}";
            var vr = new ValueRange { Values = new List<IList<object>> { PendingToRow(sub) } };
            var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req.ExecuteAsync();
            return true;
        }

        public async Task<bool> DeletePendingAsync(int id)
        {
            var (rowIndex, sub) = await FindPendingRowIndex(id);
            if (sub == null) return false;
            return await DeleteRowFromTab(TAB_PENDING, rowIndex);
        }

        private async Task<(int RowIndex, PendingSubmission? Sub)> FindPendingRowIndex(int id)
        {
            var range = $"{TAB_PENDING}!A2:Y";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count > 0 && int.TryParse(r[0]?.ToString(), out var rowId) && rowId == id)
                    return (i + 1, MapPendingRow(r));
            }
            return (0, null);
        }

        private static PendingSubmission MapPendingRow(IList<object> r)
        {
            string Get(int i) => i < r.Count ? r[i]?.ToString()?.Trim() ?? "" : "";
            return new PendingSubmission
            {
                Id = int.TryParse(Get(0), out var id) ? id : 0,
                FullName = Get(1),
                Sex = Get(2),
                DateOfBirth = Get(3),
                PresentAddress = Get(4),
                ContactNumber = Get(5),
                Email = Get(6),
                YearEnrolled = Get(7),
                YearGraduated = Get(8),
                GraduateSchoolProgram = Get(9),
                PassedLicensureExam = Get(10),
                MonthTaken = Get(11),
                YearTaken = Get(12),
                PasserStatus = Get(13),
                Awards = Get(14),
                JobTitle = Get(15),
                CompanyName = Get(16),
                Industry = Get(17),
                EmploymentType = Get(18),
                JobLocation = Get(19),
                Status = Get(20),
                RejectionReason = Get(21),
                SubmittedAt = DateTime.TryParse(Get(22), out var sa) ? sa : DateTime.UtcNow,
                ReviewedAt = DateTime.TryParse(Get(23), out var ra) ? ra : null,
                ReviewedBy = Get(24)
            };
        }

        private static IList<object> PendingToRow(PendingSubmission s) => new List<object>
        {
            s.Id, s.FullName, s.Sex, s.DateOfBirth, s.PresentAddress,
            s.ContactNumber, s.Email, s.YearEnrolled, s.YearGraduated,
            s.GraduateSchoolProgram, s.PassedLicensureExam, s.MonthTaken,
            s.YearTaken, s.PasserStatus, s.Awards, s.JobTitle, s.CompanyName,
            s.Industry, s.EmploymentType, s.JobLocation,
            s.Status, s.RejectionReason ?? "",
            s.SubmittedAt.ToString("O"),
            s.ReviewedAt?.ToString("O") ?? "",
            s.ReviewedBy ?? ""
        };

        // ═══════════════════════════════════════════════════════
        //  ADMIN USERS (new tab: AdminUsers)
        //  Columns: A=Id, B=Username, C=PasswordHash, D=FullName,
        //           E=Role, F=CreatedAt, G=IsActive
        // ═══════════════════════════════════════════════════════

        public async Task<List<AdminUser>> GetAllAdminsAsync()
        {
            var range = $"{TAB_ADMIN}!A2:G";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            return rows.Where(r => r.Count > 0).Select(MapAdminRow).ToList();
        }

        public async Task<AdminUser?> GetAdminByUsernameAsync(string username)
        {
            var all = await GetAllAdminsAsync();
            return all.FirstOrDefault(a =>
                a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<AdminUser> CreateAdminAsync(AdminUser admin)
        {
            var all = await GetAllAdminsAsync();
            admin.Id = all.Count > 0 ? all.Max(a => a.Id) + 1 : 1;

            var vr = new ValueRange { Values = new List<IList<object>> { AdminToRow(admin) } };
            var req = _service.Spreadsheets.Values.Append(vr, _spreadsheetId, TAB_ADMIN);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            req.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await req.ExecuteAsync();
            return admin;
        }

        public async Task<bool> UpdateAdminAsync(AdminUser admin)
        {
            var (rowIndex, _) = await FindAdminRowIndex(admin.Id);
            if (rowIndex == 0) return false;

            int sheetRow = rowIndex + 1;
            var range = $"{TAB_ADMIN}!A{sheetRow}:G{sheetRow}";
            var vr = new ValueRange { Values = new List<IList<object>> { AdminToRow(admin) } };
            var req = _service.Spreadsheets.Values.Update(vr, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req.ExecuteAsync();
            return true;
        }

        public async Task<bool> DeleteAdminAsync(int id)
        {
            var (rowIndex, admin) = await FindAdminRowIndex(id);
            if (admin == null || admin.Role == "superadmin") return false;
            return await DeleteRowFromTab(TAB_ADMIN, rowIndex);
        }

        private async Task<(int RowIndex, AdminUser? Admin)> FindAdminRowIndex(int id)
        {
            var range = $"{TAB_ADMIN}!A2:G";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
            var rows = response.Values ?? new List<IList<object>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count > 0 && int.TryParse(r[0]?.ToString(), out var rowId) && rowId == id)
                    return (i + 1, MapAdminRow(r));
            }
            return (0, null);
        }

        private static AdminUser MapAdminRow(IList<object> r)
        {
            string Get(int i) => i < r.Count ? r[i]?.ToString()?.Trim() ?? "" : "";
            return new AdminUser
            {
                Id = int.TryParse(Get(0), out var id) ? id : 0,
                Username = Get(1),
                PasswordHash = Get(2),
                FullName = Get(3),
                Role = Get(4),
                CreatedAt = DateTime.TryParse(Get(5), out var ca) ? ca : DateTime.UtcNow,
                IsActive = !Get(6).Equals("false", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static IList<object> AdminToRow(AdminUser a) => new List<object>
        {
            a.Id, a.Username, a.PasswordHash, a.FullName,
            a.Role, a.CreatedAt.ToString("O"), a.IsActive.ToString().ToLower()
        };

        // ═══════════════════════════════════════════════════════
        //  SHARED HELPER — delete a row from any tab
        // ═══════════════════════════════════════════════════════

        private async Task<bool> DeleteRowFromTab(string tabName, int rowIndex)
        {
            var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == tabName);
            if (sheet == null) return false;

            int sheetId = (int)sheet.Properties.SheetId!;
            int startIndex = rowIndex; // 0-based; rowIndex 1 = sheet row 2

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

            await _service.Spreadsheets.BatchUpdate(batchReq, _spreadsheetId).ExecuteAsync();
            return true;
        }
    }
}