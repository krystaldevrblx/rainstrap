using System.Net.Http;
using System.Text;

using Bloxstrap.Models.APIs.RainHub;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>Why a pairing attempt failed (safe to show to users).</summary>
    public enum RainHubPairFailureKind
    {
        /// <summary>DNS/connection failure or timeout — RainHub could not be reached.</summary>
        Unreachable,

        /// <summary>The server explicitly rejected the code (invalid/expired/consumed).</summary>
        InvalidOrExpiredCode,

        /// <summary>The server rejected the request for another reason (4xx/5xx).</summary>
        Rejected,

        /// <summary>HTTP 2xx but the body was not a usable pairing response.</summary>
        UnexpectedResponse,
    }

    /// <summary>
    /// Pairing failure with a user-safe classification. The Message never
    /// contains tokens, headers or credentials — only status codes and the
    /// server's own error code.
    /// </summary>
    public class RainHubPairingException : Exception
    {
        public RainHubPairFailureKind Kind { get; }

        /// <summary>Machine-readable error code returned by the API, if any (e.g. "invalid_or_expired_code").</summary>
        public string? ServerErrorCode { get; }

        /// <summary>HTTP status code, if a response was received.</summary>
        public int? StatusCode { get; }

        public RainHubPairingException(RainHubPairFailureKind kind, string message, int? statusCode = null, string? serverErrorCode = null, Exception? inner = null)
            : base(message, inner)
        {
            Kind = kind;
            ServerErrorCode = serverErrorCode;
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Thin HTTP wrapper for the RainHub device API.
    ///
    /// Every call is best-effort: network failures are expected (offline-first)
    /// and are surfaced as exceptions for the caller to swallow/log. This class
    /// never sends anything beyond status/configuration data — in particular it
    /// can never read or transmit Roblox cookies, which live exclusively in
    /// CookiesManager/AccountsManager and are never passed here.
    /// </summary>
    public static class RainHubClient
    {
        private const string LOG_IDENT = "RainHubClient";

        /// <summary>
        /// Retired domain from early builds — never used, auto-migrated away.
        /// Kept only so existing installations with this stale value persisted in
        /// RainHubLink.json are silently migrated to DefaultApiBase.
        /// </summary>
        private const string LegacyApiBase = "https://rainhub.app"; // canonical-domain-allow: legacy migration guard

#if DEBUG
        /// <summary>Local development API (RainHub frontend runs on 3000, API on 3001).</summary>
        public const string DefaultApiBase = "http://localhost:3001";
#else
        /// <summary>Production RainHub API endpoint (Render deployment used by keep-alive).</summary>
        public const string DefaultApiBase = "https://rainhub-api.onrender.com";
#endif

        /// <summary>
        /// Resolves the API base: an explicit non-stale override wins; otherwise
        /// the per-build default. Never returns a trailing slash. Malformed
        /// overrides fall back to the default (logged) rather than throwing.
        /// </summary>
        public static string GetApiBase()
        {
            string configured = App.RainHubLink.Prop.ApiBase?.Trim() ?? "";

            if (
                !string.IsNullOrEmpty(configured) &&
                !configured.Equals(LegacyApiBase, StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(configured, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            )
                return configured.TrimEnd('/');

            if (!string.IsNullOrEmpty(configured))
                App.Logger.WriteLine(LOG_IDENT, $"Ignoring invalid RainHubLink.ApiBase override; using built-in default");

            return DefaultApiBase;
        }

        public static async Task<PairResponse?> PairAsync(PairRequest request)
        {
            string url = $"{GetApiBase()}/api/devices/pair";
            App.Logger.WriteLine(LOG_IDENT, $"Pairing against {new Uri(url).Host}");

            using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await App.HttpClient.PostAsync(url, content);
            }
            catch (HttpRequestException ex)
            {
                throw new RainHubPairingException(RainHubPairFailureKind.Unreachable, $"Unable to reach RainHub ({ex.Message})", inner: ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new RainHubPairingException(RainHubPairFailureKind.Unreachable, "RainHub did not respond in time", inner: ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                string? errorCode = TryReadErrorCode(response);
                App.Logger.WriteLine(LOG_IDENT, $"Pairing rejected: HTTP {(int)response.StatusCode}{(errorCode is null ? "" : $" ({errorCode})")}");

                if ((int)response.StatusCode == 400 && errorCode == "invalid_or_expired_code")
                    throw new RainHubPairingException(
                        RainHubPairFailureKind.InvalidOrExpiredCode,
                        "Invalid or expired pairing code",
                        (int?)response.StatusCode,
                        errorCode
                    );

                throw new RainHubPairingException(
                    RainHubPairFailureKind.Rejected,
                    $"Pairing rejected by server (HTTP {(int)response.StatusCode})",
                    (int?)response.StatusCode,
                    errorCode
                );
            }

            PairResponse? parsed;
            try
            {
                parsed = await JsonSerializer.DeserializeAsync<PairResponse>(
                    await response.Content.ReadAsStreamAsync()
                );
            }
            catch (JsonException ex)
            {
                throw new RainHubPairingException(RainHubPairFailureKind.UnexpectedResponse, "Unexpected server response (malformed JSON)", inner: ex);
            }

            // Sanity-check the contract before handing anything to the caller.
            if (
                parsed is null ||
                string.IsNullOrEmpty(parsed.DeviceId) ||
                string.IsNullOrEmpty(parsed.DeviceToken)
            )
                throw new RainHubPairingException(RainHubPairFailureKind.UnexpectedResponse, "Unexpected server response (missing device/token fields)");

            return parsed;
        }

        public static async Task<HeartbeatResponse?> HeartbeatAsync(string deviceToken, HeartbeatRequest request)
        {
            string url = $"{GetApiBase()}/api/device/heartbeat";

            using var message = CreateAuthorizedMessage(HttpMethod.Post, url, deviceToken, JsonSerializer.Serialize(request));

            HttpResponseMessage response = await App.HttpClient.SendAsync(message);

            // 401/403 means the link is dead (revoked/disconnected) — surface via
            // HttpRequestException so the manager can disable itself.
            response.EnsureSuccessStatusCode();

            return await JsonSerializer.DeserializeAsync<HeartbeatResponse>(
                await response.Content.ReadAsStreamAsync()
            );
        }

        public static async Task AcknowledgeAsync(string deviceToken, SyncAckRequest request)
        {
            string url = $"{GetApiBase()}/api/device/sync-ack";

            using var message = CreateAuthorizedMessage(HttpMethod.Post, url, deviceToken, JsonSerializer.Serialize(request));

            HttpResponseMessage response = await App.HttpClient.SendAsync(message);
            response.EnsureSuccessStatusCode();
        }

        private static HttpRequestMessage CreateAuthorizedMessage(HttpMethod method, string url, string deviceToken, string jsonBody)
        {
            // NOTE: no `using` here — disposing the content before Send would break
            // the request. Disposing the message disposes its content.
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var message = new HttpRequestMessage(method, url) { Content = content };
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);
            return message;
        }

        /// <summary>
        /// Reads {"error": "..."} from an error body without throwing.
        /// Returns null when absent/unparseable. Only the short error CODE is
        /// ever extracted — bodies are not logged or shown verbatim.
        /// </summary>
        private static string? TryReadErrorCode(HttpResponseMessage response)
        {
            try
            {
                using var stream = response.Content.ReadAsStream();
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var el))
                {
                    string? code = el.GetString();
                    return string.IsNullOrEmpty(code) ? null : code;
                }
            }
            catch
            {
                // Non-JSON / empty error body — treated as unknown reason.
            }
            return null;
        }
    }
}
