using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionsController : ControllerBase
    {
        private readonly SubmissionService _svc;
        public SubmissionsController(SubmissionService svc) => _svc = svc;

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Submit([FromBody] PendingSubmission sub)
        {
            if (string.IsNullOrWhiteSpace(sub.FullName))
                return BadRequest(new { message = "Full name is required." });
            var result = await _svc.SubmitAsync(sub);
            return Ok(new
            {
                message = "Submission received. It will be reviewed before publishing.",
                id = result.Id
            });
        }

        [HttpGet("pending")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPending()
            => Ok(await _svc.GetPendingAsync());

        [HttpGet("all")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
            => Ok(await _svc.GetAllAsync());

        [HttpGet("count")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCount()
            => Ok(new { pending = await _svc.CountPendingAsync() });

        [HttpPost("{id}/approve")]
        [AllowAnonymous]
        public async Task<IActionResult> Approve(int id)
        {
            var reviewer = User.FindFirstValue(ClaimTypes.Name) ?? "admin";
            var (ok, msg) = await _svc.ApproveAsync(id, reviewer);
            return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
        }

        [HttpPost("{id}/reject")]
        [AllowAnonymous]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest req)
        {
            var reviewer = User.FindFirstValue(ClaimTypes.Name) ?? "admin";
            var (ok, msg) = await _svc.RejectAsync(id, req.Reason, reviewer);
            return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
        }

        [HttpDelete("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Delete(int id)
        {
            bool ok = await _svc.DeleteAsync(id);
            return ok ? Ok(new { message = "Deleted." }) : NotFound();
        }
    }

    public record RejectRequest(string Reason);
}