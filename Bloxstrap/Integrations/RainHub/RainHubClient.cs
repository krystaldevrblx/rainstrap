using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>Why a RainHub call failed, in terms the UI can explain to the user.</summary>
    public enum RainHubError
    {
        None,

        /// <summary>Could not reach RainHub at all (offline, DNS, TLS, timeout).</summary>
        Unreachable,

        /// <summary>Not linked, or the stored device credential was rejected.</summary>
        NotLinked,

        /// <summary>
        /// The request carried no credential RainHub could use. RainHub's data endpoints
        /// accept either a signed-in web session or a paired device token, so this means
        /// the request was effectively anonymous rather than that the device is
        /// insufficient. Relinking will not help.
        /// </summary>
        DeviceNotAccepted,

        /// <summary>RainHub accepted the request but refused it.</summary>
        Forbidden,

        /// <summary>RainHub is rate limiting this client.</summary>
        RateLimited,

        /// <summary>RainHub returned 5xx or is in maintenance.</summary>
        ServiceUnavailable,

        /// <summary>RainHub returned a 4xx with a machine readable error code.</summary>
        RequestRejected,

        /// <summary>The response did not match the documented contract.</summary>
        BadResponse
    }

    /// <summary>
    /// Outcome of a RainHub call. Deliberately not an exception-based API: every failure
    /// mode is something the RainHub tab has to render, not crash on.
    /// </summary>
    public readonly struct RainHubResult<T>
    {
        public bool Success { get; }
        public T? Value { get; }
        public RainHubError Error { get; }

        /// <summary>Safe to show to the user. Never contains credentials.</summary>
        public string? Message { get; }

        private RainHubResult(bool success, T? value, RainHubError error, string? message)
        {
            Success = success;
            Value = value;
            Error = error;
            Message = message;
        }

        public static RainHubResult<T> Ok(T value) => new(true, value, RainHubError.None, null);

        public static RainHubResult<T> Fail(RainHubError error, string? message = null)
            => new(false, default, error, message);
    }

    /// <summary>
    /// Thin client for the RainHub API.
    ///
    /// Design rules:
    ///  * RainHub owns all Roblox data and server-selection logic. This class only
    ///    performs HTTP and deserialises; it never re-implements filtering or sorting.
    ///  * The device credential is attached as a bearer token and is never logged.
    ///  * A dedicated HttpClient is used rather than App.HttpClient so RainHub traffic
    ///    gets its own timeout policy and is never mixed into Roblox request handling.
    ///  * Every request takes a CancellationToken so the tab can cancel in-flight work
    ///    when the user navigates away.
    /// </summary>
    public static class RainHubClient
    {
        private const string LOG_IDENT = "RainHubClient";

        /// <summary>Production RainHub API host. Overridable for self-hosting.</summary>
        public const string DefaultApiBaseUrl = "https://rainhub-api.onrender.com";

        /// <summary>Canonical public RainHub website (where accounts are managed).</summary>
        public const string WebsiteUrl = "https://getrainhub.com";

        /// <summary>Where a user generates a pairing code after signing in.</summary>
        public const string DevicesUrl = "https://getrainhub.com/dashboard/devices";

        private static readonly HttpClient Http = CreateClient();

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            })
            {
                // RainHub answers fast; a long hang would freeze the tab's spinner.
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{App.ProjectName}/{App.Version}");
            return client;
        }

        private static string BaseUrl
        {
            get
            {
                string configured = App.Settings.Prop.RainHubApiBaseUrl;

                if (string.IsNullOrWhiteSpace(configured))
                    return DefaultApiBaseUrl;

                return configured.TrimEnd('/');
            }
        }

        public static bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);

        #region Public endpoints

        /// <summary>
        /// GET /api/live-signals - the only RainHub data endpoint that is public, so it
        /// works whether or not this installation is linked.
        /// </summary>
        public static async Task<RainHubResult<RainHubLiveSignalsResponse>> GetLiveSignalsAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetAsync<RainHubLiveSignalsResponse>("/api/live-signals", requiresLink: false, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/status - used to tell "RainHub is down" apart from "not linked".
        /// </summary>
        public static async Task<RainHubResult<bool>> PingAsync(CancellationToken cancellationToken = default)
        {
            var result = await GetAsync<JsonDocument>("/api/status", requiresLink: false, cancellationToken)
                .ConfigureAwait(false);

            return result.Success
                ? RainHubResult<bool>.Ok(true)
                : RainHubResult<bool>.Fail(result.Error, result.Message);
        }

        #endregion

        #region Linked endpoints

        /// <summary>
        /// POST /api/devices/pair - exchanges a short pairing code (generated by the user
        /// in their signed-in RainHub web dashboard) for a scoped device credential.
        ///
        /// This is the public, device-facing half of RainHub's existing pairing flow.
        /// Rainstrap never asks for, transmits or stores a RainHub password.
        /// </summary>
        public static async Task<RainHubResult<RainHubPairResponse>> PairAsync(
            string code,
            string deviceName,
            CancellationToken cancellationToken = default)
        {
            var body = new RainHubPairRequest
            {
                Code = code.Trim().ToUpperInvariant(),
                DeviceName = deviceName,
                AppVersion = App.Version,
                Channel = App.Settings.Prop.Channel,
                Platform = "windows"
            };

            return await PostAsync<RainHubPairRequest, RainHubPairResponse>(
                "/api/devices/pair", body, requiresLink: false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>GET /api/search - Roblox game search, resolved by RainHub.</summary>
        public static async Task<RainHubResult<List<RainHubSearchGame>>> SearchGamesAsync(
            string query, CancellationToken cancellationToken = default)
        {
            string path = $"/api/search?q={Uri.EscapeDataString(query)}";
            return await GetAsync<List<RainHubSearchGame>>(path, requiresLink: true, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>GET /api/place-id - resolves a place ID or Roblox game URL to game info.</summary>
        public static async Task<RainHubResult<RainHubPlaceInfo>> ResolvePlaceAsync(
            string query, CancellationToken cancellationToken = default)
        {
            string path = $"/api/place-id?query={Uri.EscapeDataString(query)}";
            return await GetAsync<RainHubPlaceInfo>(path, requiresLink: true, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Recognises the two ways a user can name a specific game without typing its name:
        /// a bare place id, or a roblox.com game link copied out of a browser.
        ///
        /// The search endpoint matches on names, so feeding it either of those returns
        /// nothing and the tab looks broken. Detecting them here is what routes the query to
        /// <see cref="ResolvePlaceAsync"/> instead.
        ///
        /// Only well formed input is accepted. Anything ambiguous is left to search, because
        /// guessing at a place id and joining the wrong game is worse than asking again.
        /// </summary>
        public static bool TryExtractPlaceId(string? input, out string placeId)
        {
            placeId = "";

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string text = input.Trim();

            if (text.All(char.IsDigit))
            {
                if (text.Length is < 1 or > 12)
                    return false;

                placeId = text;
                return true;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Host, "www.roblox.com", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Host, "roblox.com", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Host, "www.rbx.pizza", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // /games/<placeId>/... and /<user>/<placeId> are the two link shapes Roblox emits.
            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (string.Equals(segments[i], "games", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = segments[i + 1];

                    if (candidate.Length is >= 1 and <= 12 && candidate.All(char.IsDigit))
                    {
                        placeId = candidate;
                        return true;
                    }
                }
            }

            if (segments.Length == 2 && segments[1].Length is >= 1 and <= 12 && segments[1].All(char.IsDigit))
            {
                placeId = segments[1];
                return true;
            }

            return false;
        }

        /// <summary>
        /// GET /api/servers - live server list for a place.
        ///
        /// Filtering and sorting are performed by RainHub; the values are passed through
        /// as the documented query parameters.
        /// </summary>
        public static async Task<RainHubResult<RainHubServerList>> GetServersAsync(
            string placeId,
            RainHubServerFilter filter,
            RainHubServerSort sort,
            CancellationToken cancellationToken = default)
        {
            string path = string.Format(
                CultureInfo.InvariantCulture,
                "/api/servers?placeId={0}&filter={1}&sort={2}",
                Uri.EscapeDataString(placeId),
                filter.ToString().ToLowerInvariant(),
                SortToApiValue(sort));

            return await GetAsync<RainHubServerList>(path, requiresLink: true, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>GET /api/trends - RainHub's Discovery report.</summary>
        public static async Task<RainHubResult<RainHubTrendReport>> GetTrendsAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetAsync<RainHubTrendReport>("/api/trends", requiresLink: true, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// POST /api/device/heartbeat - tells RainHub this installation is alive and
        /// reports its Roblox/channel state. Uses the device credential.
        /// </summary>
        public static async Task<RainHubResult<RainHubHeartbeatResponse>> HeartbeatAsync(
            bool robloxRunning, CancellationToken cancellationToken = default)
        {
            var body = new Dictionary<string, object?>
            {
                ["robloxRunning"] = robloxRunning,
                ["appVersion"] = App.Version,
                ["channel"] = App.Settings.Prop.Channel
            };

            return await PostAsync<Dictionary<string, object?>, RainHubHeartbeatResponse>(
                "/api/device/heartbeat", body, requiresLink: true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Server selection

        /// <summary>
        /// Applies the user's Quick Join preference to a server list RainHub returned.
        ///
        /// The preference is a *local tie-break between servers RainHub already chose to
        /// return*, not a second selection algorithm. RainHub remains authoritative.
        /// </summary>
        public static RainHubServer? PickQuickJoinServer(
            IReadOnlyList<RainHubServer> servers,
            QuickJoinPreference preference,
            Random? random = null)
        {
            if (servers is null || servers.Count == 0)
                return null;

            IEnumerable<RainHubServer> candidates = servers;

            // Full servers cannot be joined, so they are never valid quick-join targets.
            candidates = candidates.Where(s => s.CurrentPlayers < s.MaxPlayers || s.MaxPlayers <= 0);

            RainHubServer? best = candidates
                .OrderBy(s => s.Ping ?? int.MaxValue)
                .ThenBy(s => s.CurrentPlayers)
                .FirstOrDefault();

            // Nothing joinable came back; let the caller surface that rather than
            // silently picking a full server.
            if (best is null)
                return null;

            return preference switch
            {
                // RainHub has no region data and no server-age data, so those
                // strategies are intentionally absent rather than faked.
                QuickJoinPreference.LowestPopulation => candidates
                    .OrderBy(s => s.CurrentPlayers)
                    .ThenBy(s => s.Ping ?? int.MaxValue)
                    .FirstOrDefault(),

                QuickJoinPreference.HighestPopulation => candidates
                    .OrderByDescending(s => s.CurrentPlayers)
                    .ThenBy(s => s.Ping ?? int.MaxValue)
                    .FirstOrDefault(),

                QuickJoinPreference.LowestLatency => candidates
                    .Where(s => s.Ping.HasValue)
                    .OrderBy(s => s.Ping!.Value)
                    .FirstOrDefault() ?? best,

                QuickJoinPreference.Balanced => best,

                QuickJoinPreference.Random => candidates
                    .OrderBy(_ => (random ?? Random.Shared).Next())
                    .FirstOrDefault(),

                _ => best
            };
        }

        private static string SortToApiValue(RainHubServerSort sort) => sort switch
        {
            RainHubServerSort.Fullness => "fullness",
            RainHubServerSort.Emptiness => "emptiness",
            RainHubServerSort.Ping => "ping",
            _ => "fullness"
        };

        #endregion

        #region HTTP plumbing

        private static async Task<RainHubResult<T>> GetAsync<T>(
            string path, bool requiresLink, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            return await SendAsync<T>(request, requiresLink, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<RainHubResult<TResponse>> PostAsync<TRequest, TResponse>(
            string path, TRequest body, bool requiresLink, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json")
            };

            return await SendAsync<TResponse>(request, requiresLink, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<RainHubResult<T>> SendAsync<T>(
            HttpRequestMessage request, bool requiresLink, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return RainHubResult<T>.Fail(
                    RainHubError.Unreachable, "The RainHub API address is not valid.");
            }

            if (requiresLink)
            {
                string? token = RainHubAccount.DeviceToken;

                if (string.IsNullOrEmpty(token))
                {
                    return RainHubResult<T>.Fail(
                        RainHubError.NotLinked, "Link your RainHub account to use this.");
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpResponseMessage response;

            try
            {
                response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller navigated away. Not an error worth surfacing.
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Request to {request.RequestUri} failed: {ex.GetType().Name}");

                return RainHubResult<T>.Fail(
                    RainHubError.Unreachable, "Could not reach RainHub. Check your internet connection.");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    if (typeof(T) == typeof(JsonDocument))
                    {
                        return RainHubResult<T>.Ok((T)(object)JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
                    }

                    try
                    {
                        T? value = await response.Content
                            .ReadFromJsonAsync<T>(Json, cancellationToken)
                            .ConfigureAwait(false);

                        if (value is null)
                        {
                            return RainHubResult<T>.Fail(
                                RainHubError.BadResponse, "RainHub returned an empty response.");
                        }

                        return RainHubResult<T>.Ok(value);
                    }
                    catch (JsonException)
                    {
                        return RainHubResult<T>.Fail(
                            RainHubError.BadResponse, "RainHub returned a response Rainstrap could not read.");
                    }
                }

                return await MapFailure<T>(response, request).ConfigureAwait(false);
            }
        }

        private static async Task<RainHubResult<T>> MapFailure<T>(
            HttpResponseMessage response, HttpRequestMessage request)        {
            string? code = null;

            try
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    var parsed = JsonSerializer.Deserialize<RainHubApiErrorBody>(body, Json);

                    if (!string.IsNullOrWhiteSpace(parsed?.Error))
                        code = parsed!.Error;
                }
            }
            catch
            {
                // A non-JSON error body is fine; the status code is what matters.
            }

            // The status code and route are safe to log. Headers are never logged, so the
            // device credential cannot reach the log file through this path.
            App.Logger.WriteLine(
                LOG_IDENT,
                $"{request.Method} {request.RequestUri} -> {(int)response.StatusCode} {code}");

            switch ((int)response.StatusCode)
            {
                case 401:
                {
                    var kind = DescribeUnauthorized(code);
                    return RainHubResult<T>.Fail(kind, UnauthorizedMessage(kind));
                }

                case 403:
                    return RainHubResult<T>.Fail(
                        RainHubError.Forbidden, DescribeForbidden(code));

                case 429:
                    return RainHubResult<T>.Fail(
                        RainHubError.RateLimited, "RainHub is busy right now. Try again shortly.");

                case 404:
                    return RainHubResult<T>.Fail(
                        RainHubError.RequestRejected,
                        string.IsNullOrWhiteSpace(code) ? "RainHub could not find that." : Humanize(code));

                default:
                    if ((int)response.StatusCode >= 500)
                    {
                        return RainHubResult<T>.Fail(
                            RainHubError.ServiceUnavailable,
                            "RainHub is unavailable right now. Rainstrap will keep working without it.");
                    }

                    return RainHubResult<T>.Fail(
                        RainHubError.RequestRejected,
                        string.IsNullOrWhiteSpace(code) ? "RainHub rejected the request." : Humanize(code));
            }
        }

        /// <summary>
        /// Distinguishes "this device needs linking again" from "this request carried no
        /// credential RainHub could use".
        ///
        /// The two look identical on the wire - both are a bare 401 - but the user's next
        /// action is completely different. RainHub answers <c>device_token_required</c> or
        /// <c>invalid_device_token</c> when the device credential itself is the problem,
        /// and a plain <c>unauthorized</c> when nothing usable was presented. Telling a
        /// correctly linked user to relink would be misleading, so they are not asked to.
        /// </summary>
        private static RainHubError DescribeUnauthorized(string? code) => code switch
        {
            "device_token_required" => RainHubError.NotLinked,
            "invalid_device_token" => RainHubError.NotLinked,

            // No recognised credential reached this route, so the request was
            // unauthenticated rather than rejected for what it carried.
            _ => RainHubError.DeviceNotAccepted
        };

        private static string UnauthorizedMessage(RainHubError error) => error switch
        {
            RainHubError.NotLinked =>
                "RainHub did not accept this device's link. Link the device again.",
            _ =>
                "RainHub did not accept this request. Live signals still work without a link."
        };

        /// <summary>
        /// The device-token path returns 403 because a linked device is not a browser
        /// session. Say what that means instead of leaking a raw code at the user.
        /// </summary>
        private static string DescribeForbidden(string? code) =>
            code == "invalid_device_token" || code == "device_revoked" || code == "device_disconnected"
                ? "This device's RainHub link is no longer valid. Link the device again."
                : "RainHub refused this request for the linked device.";

        private static string Humanize(string code) =>
            code.Replace('_', ' ').Trim();

        #endregion
    }
}
