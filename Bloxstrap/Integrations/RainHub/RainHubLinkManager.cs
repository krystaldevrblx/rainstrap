using Bloxstrap.Models.APIs.RainHub;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>
    /// Background RainHub synchronization loop.
    ///
    /// OFFLINE-FIRST CONTRACT:
    ///  * RainHub is entirely optional. When the link is disabled, this service
    ///    does nothing and Roblox launching is never affected.
    ///  * Every network failure is swallowed (with backoff) — a dead internet,
    ///    expired session or unreachable server can never block launching.
    ///  * The loop only transmits status/configuration data and only ever
    ///    receives FastFlag profile payloads, which are validated locally before
    ///    being applied.
    ///
    /// This mirrors the Watcher/ActivityWatcher topology: started via Task.Run,
    /// UI updates flow through events marshalled onto the UI thread by callers.
    /// </summary>
    public class RainHubLinkManager : IDisposable
    {
        private const string LOG_IDENT = "RainHubLinkManager";

        public static readonly RainHubLinkManager Instance = new();

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _loopTask;

        // Loop cadence, adjusted from server hints and error backoff.
        private int _intervalSeconds = 60;

        private volatile bool _lastSyncOk;

        /// <summary>True when the last heartbeat round-tripped successfully.</summary>
        public bool IsReachable => _lastSyncOk;

        /// <summary>Description of the last applied/rejected profile push (for UI).</summary>
        public string LastSyncMessage { get; private set; } = "";

        public bool IsLinked => App.RainHubLink.Prop.Enabled && !string.IsNullOrEmpty(App.RainHubLink.Prop.DeviceToken);

        /// <summary>Raised after every heartbeat cycle, on a worker thread.</summary>
        public event EventHandler? StatusChanged;

        private RainHubLinkManager() { }

        public void Start()
        {
            if (_loopTask is not null)
                return; // already running

            _cancellationTokenSource = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoop(_cancellationTokenSource.Token));

            App.Logger.WriteLine(LOG_IDENT, $"Started (linked: {IsLinked})");
        }

        public void Dispose()
        {
            try { _cancellationTokenSource?.Cancel(); } catch { }
        }

        private async Task RunLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (App.RainHubLink.Prop.Enabled && !string.IsNullOrEmpty(App.RainHubLink.Prop.DeviceToken))
                        await DoHeartbeat();
                    else
                        _lastSyncOk = false;
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == HttpStatusCode.Unauthorized ||
                    ex.StatusCode == HttpStatusCode.Forbidden
                )
                {
                    // The device was disconnected or revoked from the dashboard.
                    // Stop heartbeating but keep local state intact so the user can
                    // clean up or re-link from the settings page. Never fatal.
                    _lastSyncOk = false;
                    App.Logger.WriteLine(LOG_IDENT, "Link rejected by RainHub (disconnected or revoked) — disabling sync");

                    App.RainHubLink.Prop.Enabled = false;
                    App.RainHubLink.Save();

                    LastSyncMessage = "Link was revoked from the RainHub dashboard";
                }
                catch (Exception ex)
                {
                    _lastSyncOk = false;

                    // Expected when offline / server down. Back off instead of hammering;
                    // this must never be fatal.
                    _intervalSeconds = 300;
                    App.Logger.WriteLine(LOG_IDENT, $"Heartbeat failed (will retry in {_intervalSeconds}s): {ex.Message}");
                }

                try { StatusChanged?.Invoke(this, EventArgs.Empty); }
                catch { /* never let UI notification kill the loop */ }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task DoHeartbeat()
        {
            string token = App.RainHubLink.Prop.DeviceToken;

            var request = new HeartbeatRequest
            {
                RobloxRunning = Utilities.IsRobloxRunning(),
                AppVersion = App.Version,
                Channel = App.Settings.Prop.Channel,
            };

            HeartbeatResponse? response = await RainHubClient.HeartbeatAsync(token, request);

            if (response is null)
                throw new InvalidDataException("RainHub returned an empty heartbeat");

            _lastSyncOk = true;
            _intervalSeconds = Math.Clamp(response.HeartbeatIntervalSeconds, 30, 600);

            var pending = response.PendingAction;
            if (pending is null || pending.Type != "apply_profile")
            {
                LastSyncMessage = $"Last sync ok ({DateTime.Now:h:mm:ss tt})";
                return;
            }

            App.Logger.WriteLine(
                LOG_IDENT,
                $"Received profile push '{pending.ProfileName}' v{pending.VersionNumber} ({pending.Stability})"
            );

            var result = RainHubProfileApplier.Apply(pending);

            await RainHubClient.AcknowledgeAsync(token, new SyncAckRequest
            {
                Ok = result.Success,
                Error = result.Error,
                ProfileId = pending.ProfileId,
                ProfileName = pending.ProfileName,
                VersionId = pending.VersionId,
                RobloxRunning = Utilities.IsRobloxRunning(),
            });

            if (result.Success)
            {
                LastSyncMessage = $"Applied '{pending.ProfileName}' v{pending.VersionNumber}";
                Frontend.ShowBalloonTip(
                    "RainHub",
                    $"FastFlag profile '{pending.ProfileName}' was applied. Use RainHub settings to roll back if needed.",
                    System.Windows.Forms.ToolTipIcon.Info
                );
            }
            else
            {
                LastSyncMessage = result.Error;
                Frontend.ShowBalloonTip(
                    "RainHub",
                    $"Rejected profile '{pending.ProfileName}': {result.Error}",
                    System.Windows.Forms.ToolTipIcon.Warning
                );
            }
        }
    }
}
