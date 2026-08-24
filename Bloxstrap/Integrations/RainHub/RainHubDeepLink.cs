using System.Web;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>
    /// Strict parser for rainhub:// deep links.
    ///
    /// SECURITY CONTRACT: the ONLY supported link is
    ///
    ///     rainhub://join?placeId={numeric}&gameInstanceId={server guid}
    ///
    /// Everything else is rejected. A deep link can therefore never trigger
    /// arbitrary commands, arbitrary URLs, file access or configuration changes —
    /// it can only ask Rainstrap to launch Roblox into a specific public server,
    /// exactly like the roblox-player:// protocol does.
    /// </summary>
    public static class RainHubDeepLink
    {
        public const string ProtocolName = "rainhub";

        public class JoinRequest
        {
            public long PlaceId { get; init; }
            public string JobId { get; init; } = "";
        }

        private static readonly Regex PlaceIdPattern = new("^[0-9]{1,16}$", RegexOptions.Compiled);
        private static readonly Regex JobIdPattern = new("^[a-zA-Z0-9-]{16,64}$", RegexOptions.Compiled);

        public static bool TryParse(string? uriString, out JoinRequest? request)
        {
            request = null;

            if (string.IsNullOrWhiteSpace(uriString))
                return false;

            if (!uriString.StartsWith($"{ProtocolName}://", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                // Uri parses custom schemes reliably when prefixed this way.
                Uri uri = new(uriString);

                // host must be "join" (rainhub://join?query or rainhub://join/path?query)
                string host = uri.Host.ToLowerInvariant();

                if (host != "join")
                    return false;

                var query = HttpUtility.ParseQueryString(uri.Query);

                string? placeIdRaw = query["placeId"];
                string? jobIdRaw = query["gameInstanceId"];

                if (string.IsNullOrEmpty(placeIdRaw) || string.IsNullOrEmpty(jobIdRaw))
                    return false;

                if (!PlaceIdPattern.IsMatch(placeIdRaw) || !JobIdPattern.IsMatch(jobIdRaw))
                    return false;

                request = new JoinRequest
                {
                    PlaceId = long.Parse(placeIdRaw),
                    JobId = jobIdRaw,
                };
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RainHubDeepLink::TryParse", $"Rejected malformed link: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds the standard roblox-player launch command for a server join,
        /// reusing the exact pipeline used by the roblox/roblox-player protocols.
        /// </summary>
        public static string BuildRobloxPlayerCommand(JoinRequest join)
        {
            string placeLauncherUrl = UrlBuilder.BuildPlacelauncherUrl(join.PlaceId, join.JobId);
            return $"roblox-player:1+launchmode:game+task:LaunchGame+placelauncherurl:{HttpUtility.UrlEncode(placeLauncherUrl)}+";
        }
    }
}
