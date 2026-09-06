namespace Bloxstrap.Utility
{
    internal static class VersionChecker
    {
        private const string RemoteVersionUrl = "https://raw.githubusercontent.com/krystaldevrblx/rainstrap/main/version.json";

        private static string? _localVersion;

        public static string LocalVersion
        {
            get
            {
                if (_localVersion is null)
                {
                    string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");

                    if (File.Exists(versionFile))
                    {
                        string json = File.ReadAllText(versionFile);
                        var info = JsonSerializer.Deserialize<Models.APIs.VersionInfo>(json);
                        _localVersion = info?.Version ?? App.Version;
                    }
                    else
                    {
                        _localVersion = App.Version;
                    }
                }

                return _localVersion;
            }
        }

        public static async Task<(bool UpdateAvailable, string LatestVersion)> CheckForUpdateAsync()
        {
            const string LOG_IDENT = "VersionChecker::CheckForUpdateAsync";

            try
            {
                Uri remoteVersionUri = new(RemoteVersionUrl);
                var remoteInfo = await Http.GetJson<Models.APIs.VersionInfo>(remoteVersionUri);

                if (remoteInfo is null || string.IsNullOrEmpty(remoteInfo.Version))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Remote version info is null or empty");
                    return (false, LocalVersion);
                }

                string localVer = LocalVersion;
                string remoteVer = remoteInfo.Version;

                App.Logger.WriteLine(LOG_IDENT, $"Local version: {localVer}, Remote version: {remoteVer}");

                var comparison = Utilities.CompareVersions(localVer, remoteVer);

                bool updateAvailable = comparison == VersionComparison.LessThan;

                return (updateAvailable, remoteVer);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to check for update");
                App.Logger.WriteException(LOG_IDENT, ex);
                return (false, LocalVersion);
            }
        }
    }
}
