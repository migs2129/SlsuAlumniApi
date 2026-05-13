using AlumniTrackingAPI.Services;

namespace AlumniTrackingAPI.BackgroundServices
{
    // ── Automatic polling every 5 minutes ────────────────────────────────
    // Runs SyncService in the background so new Google Form responses
    // are automatically moved to SQLite pending without admin action.
    // Change SYNC_INTERVAL_MINUTES to adjust how often it polls.
    public class SyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<SyncBackgroundService> _log;
        private const int SYNC_INTERVAL_MINUTES = 5;

        public SyncBackgroundService(
            IServiceProvider services, 
            ILogger<SyncBackgroundService> log)
        {
            _services = services;
            _log      = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _log.LogInformation(
                "[SyncBackground] Started. Will sync every {M} minutes.",
                SYNC_INTERVAL_MINUTES);

            // Wait 30 seconds after startup before first sync
            // (gives the app time to fully initialize)
            await Task.Delay(TimeSpan.FromSeconds(30), ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope   = _services.CreateScope();
                    var syncService   = scope.ServiceProvider.GetRequiredService<SyncService>();
                    var result        = await syncService.SyncFromSheetAsync();

                    if (result.Imported > 0)
                        _log.LogInformation(
                            "[SyncBackground] Auto-sync: {I} new submissions imported.",
                            result.Imported);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[SyncBackground] Auto-sync failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(SYNC_INTERVAL_MINUTES), ct);
            }
        }
    }
}
