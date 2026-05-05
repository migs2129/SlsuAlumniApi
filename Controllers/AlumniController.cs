using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlumniController : ControllerBase
    {
        private readonly GoogleSheetsService _sheets;
        public AlumniController(GoogleSheetsService sheets) => _sheets = sheets;

        // ── PUBLIC ─────────────────────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
            => Ok(await _sheets.GetAllAsync());

        [HttpGet("year/{year}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByYear(string year)
            => Ok(await _sheets.GetByYearGraduatedAsync(year));

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string keyword)
            => Ok(await _sheets.SearchByNameAsync(keyword));

        [HttpGet("analytics/summary")]
        [AllowAnonymous]
        public async Task<IActionResult> Summary()
            => Ok(await _sheets.GetSummaryAsync());

        [HttpGet("analytics/graduates-per-year")]
        [AllowAnonymous]
        public async Task<IActionResult> GraduatesPerYear()
            => Ok(await _sheets.GetGraduatesPerYearAsync());

        [HttpGet("analytics/employment-breakdown")]
        [AllowAnonymous]
        public async Task<IActionResult> EmploymentBreakdown()
            => Ok(await _sheets.GetEmploymentBreakdownAsync());

        [HttpGet("analytics/industry-breakdown")]
        [AllowAnonymous]
        public async Task<IActionResult> IndustryBreakdown()
            => Ok(await _sheets.GetIndustryBreakdownAsync());

        [HttpGet("analytics/rme-passing-rate")]
        [AllowAnonymous]
        public async Task<IActionResult> RmePassingRate()
            => Ok(await _sheets.GetRmePassingRateAsync());

        // ── ADMIN WRITE — direct to Google Sheets, bypasses pending ────────
        // FIX: Admin-added records go DIRECTLY to Sheets, NOT to pending queue.
        // The pending queue is only for sync'd Google Form submissions.
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Add([FromBody] Alumni alumni)
        {
            if (string.IsNullOrWhiteSpace(alumni.FullName))
                return BadRequest(new { message = "Full name is required." });

            // Stamp timestamp and agreement since admin is adding directly
            alumni.Timestamp = DateTime.UtcNow.ToString("M/d/yyyy H:mm:ss");
            alumni.Agreement = "I Agree";
            alumni.PrivacyConsent = "Yes";

            bool ok = await _sheets.AddAlumniAsync(alumni);
            return ok
                ? Ok(new { message = "Alumni added directly to Google Sheets." })
                : StatusCode(500, new { message = "Failed to write to Google Sheets." });
        }

        [HttpPut("{rowIndex:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Update(int rowIndex, [FromBody] Alumni alumni)
        {
            if (rowIndex < 1)
                return BadRequest(new { message = "Row index must be 1 or greater." });
            bool ok = await _sheets.UpdateAlumniAsync(rowIndex, alumni);
            return ok
                ? Ok(new { message = "Record updated." })
                : StatusCode(500, new { message = "Failed to update row." });
        }

        [HttpDelete("{rowIndex:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Delete(int rowIndex)
        {
            if (rowIndex < 1)
                return BadRequest(new { message = "Row index must be 1 or greater." });
            bool ok = await _sheets.DeleteAlumniAsync(rowIndex);
            return ok
                ? Ok(new { message = "Record deleted." })
                : StatusCode(500, new { message = "Failed to delete row." });
        }
        [HttpGet("analytics/top-notchers")]
        public async Task<IActionResult> GetTopNotchers([FromQuery] string year, [FromQuery] string month)
        {
            var all = await _sheets.GetAllAsync();

            var result = all
                .Where(a => a.YearTaken == year && a.MonthTaken == month)
                .Where(a => !string.IsNullOrEmpty(a.Awards))
                .Where(a => a.Awards.Contains("Top"))
                .ToList();

            return Ok(result);
        }
    }
}
