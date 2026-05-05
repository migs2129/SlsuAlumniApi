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

        // ── PUBLIC — returns published results with narrative ──────────────
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublished()
        {
            try
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
                    e.TopNotchers,
                    narrative = ExamResultService.GenerateNarrative(e)
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExamResults] GetPublished error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── ADMIN — all results including drafts ───────────────────────────
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _svc.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, new { message = "Server error", detail = ex.Message });
            }
        }

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
                result.TopNotchers,
                narrative = ExamResultService.GenerateNarrative(result)
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] ExamResult result)
        {
            try
            {
                Console.WriteLine($"TopNotchers: {result.TopNotchers}");

                var created = await _svc.CreateAsync(result);
                return Ok(created);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Update(int id, [FromBody] ExamResult result)
        {
            try
            {
                var updated = await _svc.UpdateAsync(id, result);
                return updated == null ? NotFound() : Ok(updated);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExamResults] Update error: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPatch("{id:int}/toggle-publish")]
        [AllowAnonymous]
        public async Task<IActionResult> TogglePublish(int id)
        {
            bool ok = await _svc.TogglePublishedAsync(id);
            return ok ? Ok(new { message = "Publish status toggled." }) : NotFound();
        }

        [HttpDelete("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Delete(int id)
        {
            bool ok = await _svc.DeleteAsync(id);
            return ok ? Ok(new { message = "Deleted." }) : NotFound();
        }

        // ── Preview system data without saving ────────────────────────────
        [HttpGet("preview-system")]
        [AllowAnonymous]
        public async Task<IActionResult> PreviewSystem(
            [FromQuery] string month, [FromQuery] int year)
        {
            if (string.IsNullOrWhiteSpace(month) || year == 0)
                return BadRequest(new { message = "month and year are required." });

            try
            {
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
                    note = "National data must be entered manually.",
                    narrative = ExamResultService.GenerateNarrative(result)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExamResults] PreviewSystem error: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}