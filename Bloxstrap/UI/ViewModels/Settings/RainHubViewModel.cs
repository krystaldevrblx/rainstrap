using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Integrations.RainHub;
using Bloxstrap.UI.ViewModels;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class RainHubViewModel : NotifyPropertyChangedViewModel
    {
        // ─── Bindable state ─────────────────────────────────────────────────────

        public bool IsLinked
        {
            get => App.RainHubLink.Prop.Enabled && !string.IsNullOrEmpty(App.RainHubLink.Prop.DeviceToken);
        }

        /// <summary>
        /// 6-character pairing code shown on the RainHub dashboard.
        /// </summary>
        public string PairingCode { get; set; } = "";

        public string DeviceName { get; set; } = Environment.MachineName;

        public string StatusText { get; set; } = BuildStatusText();

        public bool HasBackup => RainHubProfileApplier.HasBackup;

        public bool IsPairing { get; private set; }

        public Visibility LinkedPanelVisibility => IsLinked ? Visibility.Visible : Visibility.Collapsed;

        public Visibility UnlinkedPanelVisibility => IsLinked ? Visibility.Collapsed : Visibility.Visible;

        // ─── Commands ───────────────────────────────────────────────────────────

        public IAsyncRelayCommand LinkCommand => new AsyncRelayCommand(async () => await LinkAsync());

        public ICommand UnlinkCommand => new RelayCommand(() => Unlink());

        public ICommand RollbackCommand => new RelayCommand(() =>
        {
            string? error = RainHubProfileApplier.Rollback();
            
            if (error is null)
                Frontend.ShowMessageBox(
                    "Your previous FastFlag configuration was restored.",
                    MessageBoxImage.Information
                );
            else
                Frontend.ShowMessageBox($"Could not roll back: {error}", MessageBoxImage.Warning);

            OnPropertyChanged(nameof(HasBackup));
        });

        public ICommand OpenDevicesPageCommand => new RelayCommand(() =>
        {
            Utilities.ShellExecute("https://getrainhub.com/dashboard/devices");
        });

        // ─── Logic ──────────────────────────────────────────────────────────────

        private async Task LinkAsync()
        {
            const string LOG_IDENT = "RainHubViewModel::LinkAsync";

            if (IsPairing)
                return;

            string code = PairingCode.Trim().ToUpperInvariant();

            if (!Regex.IsMatch(code, "^[A-Z0-9]{6}$"))
            {
                Frontend.ShowMessageBox(
                    "Enter the 6-character code shown on the RainHub devices page.",
                    MessageBoxImage.Warning
                );
                return;
            }

            IsPairing = true;
            StatusText = "Connecting to RainHub…";
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsPairing));

            try
            {
                var response = await RainHubClient.PairAsync(new Models.APIs.RainHub.PairRequest
                {
                    Code = code,
                    DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName.Trim(),
                    AppVersion = App.Version,
                    Channel = App.Settings.Prop.Channel,
                    Platform = "windows",
                });

                if (response is null || string.IsNullOrEmpty(response.DeviceToken))
                    throw new InvalidDataException("RainHub returned an empty pairing response");

                App.RainHubLink.Prop.Enabled = true;
                App.RainHubLink.Prop.DeviceId = response.DeviceId;
                App.RainHubLink.Prop.DeviceToken = response.DeviceToken;
                App.RainHubLink.Prop.DeviceName = DeviceName.Trim();
                App.RainHubLink.Prop.LinkedAt = DateTime.Now;
                App.RainHubLink.Save();

                StatusText = "Linked! This device will now appear on your RainHub dashboard.";
                // Log the public device id only — never the token.
                App.Logger.WriteLine(LOG_IDENT, $"Paired as device '{response.DeviceId}'");
            }
            catch (RainHubPairingException ex)
            {
                // ex.Message is user-safe by construction: status codes and server
                // error codes only — never tokens, headers or bodies.
                App.Logger.WriteLine(LOG_IDENT, $"Pairing failed: {ex.Kind} (status={ex.StatusCode?.ToString() ?? "-"}, serverCode={ex.ServerErrorCode ?? "-"})");

                StatusText = PairingFailureText(ex);
                OnPropertyChanged(nameof(StatusText));

                Frontend.ShowMessageBox(
                    $"{PairingFailureText(ex)}\n\nRainstrap keeps working normally without a RainHub link.",
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);

                StatusText = "Unexpected error while pairing. See the log for details — everything still works offline.";
                OnPropertyChanged(nameof(StatusText));

                Frontend.ShowMessageBox(
                    "Unexpected error while pairing.\n\nRainstrap keeps working normally without a RainHub link.",
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                IsPairing = false;
                OnPropertyChanged(nameof(IsLinked));
                OnPropertyChanged(nameof(IsPairing));
                OnPropertyChanged(nameof(LinkedPanelVisibility));
                OnPropertyChanged(nameof(UnlinkedPanelVisibility));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(DeviceName));
            }
        }

        /// <summary>User-facing diagnostic for a failed pairing attempt.</summary>
        private static string PairingFailureText(RainHubPairingException ex)
        {
            string rejected = ex.StatusCode is int statusCode
                ? $"Pairing rejected by server (HTTP {statusCode})."
                : "Pairing rejected by server.";

            return ex.Kind switch
            {
                RainHubPairFailureKind.Unreachable =>
                    "Unable to reach RainHub. Check your internet connection and try again.",
                RainHubPairFailureKind.InvalidOrExpiredCode =>
                    "Invalid or expired pairing code. Generate a fresh code on the RainHub devices page and try again.",
                RainHubPairFailureKind.Rejected => rejected,
                RainHubPairFailureKind.UnexpectedResponse =>
                    "Unexpected server response. The RainHub API may be updating — try again shortly.",
                _ => "Pairing failed.",
            };
        }

        private void Unlink()
        {
            var result = Frontend.ShowMessageBox(
                "Disconnect this device from RainHub? Your FastFlags stay exactly as they are.",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            // Best-effort server-side disconnect is implicit: revoking happens when
            // the token stops heartbeating. Local state is cleared immediately.
            App.RainHubLink.Prop.Enabled = false;
            App.RainHubLink.Prop.DeviceToken = "";
            App.RainHubLink.Save();

            StatusText = BuildStatusText();
            OnPropertyChanged(nameof(IsLinked));
            OnPropertyChanged(nameof(LinkedPanelVisibility));
            OnPropertyChanged(nameof(UnlinkedPanelVisibility));
            OnPropertyChanged(nameof(StatusText));
        }

        private static string BuildStatusText()
        {
            if (App.RainHubLink.Prop.Enabled && !string.IsNullOrEmpty(App.RainHubLink.Prop.DeviceToken))
            {
                string status = App.RainHubManager.IsReachable ? "connected" : "offline / retrying";
                return $"This device is linked to your RainHub account ({status}).";
            }
            return "Not linked. RainHub is completely optional — everything keeps working without it.";
        }
    }
}
