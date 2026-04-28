using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly SyncService _sync;
        public SyncController(SyncService sync) => _sync = sync;

        // POST /api/sync
        // Admin manually triggers a sync from Google Sheets → SQLite pending
        // Also runs automatically on a timer (see SyncBackgroundService)
        [HttpPost]
        //[Authorize]
        [AllowAnonymous]
        public async Task<IActionResult> Sync()
        {
            var result = await _sync.SyncFromSheetAsync();

            if (result.Error != null)
                return StatusCode(500, new { message = result.Error });

            return Ok(new
            {
                message = $"Sync complete. {result.Imported} new submission(s) moved to pending.",
                totalInSheet = result.TotalInSheet,
                imported = result.Imported,
                skipped = result.Skipped,
                deleted = result.Deleted,
                deleteErrors = result.DeleteErrors
            });
        }

        // GET /api/sync/status — check when last sync ran (public, for dashboard)
        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult Status()
        {
            return Ok(new
            {
                message = "Sync endpoint is active. POST /api/sync to trigger a manual sync."
            });
        }
    }
}
