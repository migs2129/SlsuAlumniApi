using System.Text.Json;
using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamResultsController : ControllerBase
    {
        private readonly ExamResultService _svc;
        public ExamResultsController(ExamResultService svc) => _svc = svc;

        // 🔧 helper to fix your issue (STRING → ARRAY)
        private object ParseTopNotchers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<object>();

            try
            {
                return JsonSerializer.Deserialize<object>(json) ?? new List<object>();
            }
            catch
            {
                return new List<object>();
            }
        }

        // ── PUBLIC ─────────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublished()
        {
            var results = await _svc.GetPublishedAsync();

            return Ok(results.Select(e => new
            {
                e.Id,
                e.Month,
                e.Year,
                e.SlsuPassingRate,
                e.SlsuPassers,
                e.SlsuExaminees,
                e.FirstTimePassingRate,
                e.FirstTimePassers,
                e.FirstTimeExaminees,
                e.RepeaterPassingRate,
                e.RepeaterPassers,
                e.RepeaterExaminees,
                e.NationalPassingRate,
                e.NationalPassers,
                e.NationalExaminees,
                e.DifferenceFromNational,

                // ✅ FIXED HERE
                TopNotchers = ParseTopNotchers(e.TopNotchers),

                narrative = ExamResultService.GenerateNarrative(e)
            }));
        }

        // ── ADMIN ALL ─────────────────────────────────────────
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var results = await _svc.GetAllAsync();

            return Ok(results.Select(e => new
            {
                e.Id,
                e.Month,
                e.Year,
                e.DataSource,
                e.SlsuPassingRate,
                e.SlsuPassers,
                e.SlsuExaminees,
                e.FirstTimePassingRate,
                e.FirstTimePassers,
                e.FirstTimeExaminees,
                e.RepeaterPassingRate,
                e.RepeaterPassers,
                e.RepeaterExaminees,
                e.NationalPassingRate,
                e.NationalPassers,
                e.NationalExaminees,
                e.DifferenceFromNational,
                e.IsPublished,

                // ✅ FIXED HERE
                TopNotchers = ParseTopNotchers(e.TopNotchers)
            }));
        }

        // ── GET BY ID ─────────────────────────────────────────
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _svc.GetByIdAsync(id);
            if (result == null) return NotFound();

            return Ok(new
            {
                result.Id,
                result.Month,
                result.Year,
                result.DataSource,
                result.SlsuPassingRate,
                result.SlsuPassers,
                result.SlsuExaminees,
                result.FirstTimePassingRate,
                result.FirstTimePassers,
                result.FirstTimeExaminees,
                result.RepeaterPassingRate,
                result.RepeaterPassers,
                result.RepeaterExaminees,
                result.NationalPassingRate,
                result.NationalPassers,
                result.NationalExaminees,
                result.DifferenceFromNational,
                result.IsPublished,

                // ✅ FIXED HERE
                TopNotchers = ParseTopNotchers(result.TopNotchers),

                narrative = ExamResultService.GenerateNarrative(result)
            });
        }

        // ── CREATE ────────────────────────────────────────────
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] ExamResult result)
        {
            var created = await _svc.CreateAsync(result);
            return Ok(created);
        }

        // ── UPDATE ────────────────────────────────────────────
        [HttpPut("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Update(int id, [FromBody] ExamResult result)
        {
            var updated = await _svc.UpdateAsync(id, result);
            return updated == null ? NotFound() : Ok(updated);
        }

        // ── TOGGLE PUBLISH ────────────────────────────────────
        [HttpPatch("{id:int}/toggle-publish")]
        [AllowAnonymous]
        public async Task<IActionResult> TogglePublish(int id)
        {
            bool ok = await _svc.TogglePublishedAsync(id);
            return ok ? Ok(new { message = "Publish status toggled." }) : NotFound();
        }

        // ── DELETE ────────────────────────────────────────────
        [HttpDelete("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Delete(int id)
        {
            bool ok = await _svc.DeleteAsync(id);
            return ok ? Ok(new { message = "Deleted." }) : NotFound();
        }

        // ── PREVIEW SYSTEM ────────────────────────────────────
        [HttpGet("preview-system")]
        [AllowAnonymous]
        public async Task<IActionResult> PreviewSystem(
            [FromQuery] string month, [FromQuery] int year)
        {
            if (string.IsNullOrWhiteSpace(month) || year == 0)
                return BadRequest(new { message = "month and year are required." });

            var result = await _svc.PullFromSystemAsync(month, year);

            return Ok(new
            {
                result.Month,
                result.Year,
                result.SlsuPassingRate,
                result.SlsuPassers,
                result.SlsuExaminees,
                result.FirstTimePassingRate,
                result.FirstTimePassers,
                result.FirstTimeExaminees,
                result.RepeaterPassingRate,
                result.RepeaterPassers,
                result.RepeaterExaminees,

                // ✅ INCLUDE TOP NOTCHERS HERE TOO
                TopNotchers = ParseTopNotchers(result.TopNotchers),

                note = "National data must be entered manually.",
                narrative = ExamResultService.GenerateNarrative(result)
            });
        }
    }
}