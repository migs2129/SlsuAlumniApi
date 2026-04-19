using System;

using AlumniTrackingAPI.Services;
using AlumniTrackingAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlumniController : ControllerBase
    {
        private readonly GoogleSheetsService _sheets;
        public AlumniController(GoogleSheetsService sheets) => _sheets = sheets;

        // ── Data endpoints ─────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _sheets.GetAllAsync());

        [HttpGet("year/{year}")]
        public async Task<IActionResult> GetByYear(string year)
            => Ok(await _sheets.GetByYearGraduatedAsync(year));

        [HttpGet("employment/{type}")]
        public async Task<IActionResult> GetByEmployment(string type)
            => Ok(await _sheets.GetByEmploymentTypeAsync(type));

        [HttpGet("industry/{industry}")]
        public async Task<IActionResult> GetByIndustry(string industry)
            => Ok(await _sheets.GetByIndustryAsync(industry));

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string keyword)
            => Ok(await _sheets.SearchByNameAsync(keyword));

        // ── Analytics endpoints (for bar graphs) ──────────────────────────────

        [HttpGet("analytics/summary")]
        public async Task<IActionResult> Summary()
            => Ok(await _sheets.GetSummaryAsync());

        [HttpGet("analytics/graduates-per-year")]
        public async Task<IActionResult> GraduatesPerYear()
            => Ok(await _sheets.GetGraduatesPerYearAsync());

        [HttpGet("analytics/employment-breakdown")]
        public async Task<IActionResult> EmploymentBreakdown()
            => Ok(await _sheets.GetEmploymentBreakdownAsync());

        [HttpGet("analytics/industry-breakdown")]
        public async Task<IActionResult> IndustryBreakdown()
            => Ok(await _sheets.GetIndustryBreakdownAsync());

        [HttpGet("analytics/sex-breakdown")]
        public async Task<IActionResult> SexBreakdown()
            => Ok(await _sheets.GetSexBreakdownAsync());

        [HttpGet("analytics/rme-passing-rate")]
        public async Task<IActionResult> RmePassingRate()
            => Ok(await _sheets.GetRmePassingRateAsync());
    }
}