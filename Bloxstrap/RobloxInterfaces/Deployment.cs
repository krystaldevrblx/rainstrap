using Bloxstrap.Utility;
using Bloxstrap.Properties;
using System;
using System.Configuration;
using System.Windows.Automation;
using Windows.Win32.Foundation;

namespace Bloxstrap.RobloxInterfaces
{
    public static class Deployment
    {
        public const string DefaultRobloxDomain = "roblox.com";

        public const string DefaultChannel = "production";
        
        private const string VersionStudioHash = "version-012732894899482c";


        public static EventHandler<string>? ChannelChanged;
        private static string _channel = App.Settings.Prop.Channel;
        public static string Channel {
            get => _channel;
            set
            {
                _channel = value;
                App.Settings.Prop.Channel = Channel;
                App.Settings.Save();

                ChannelChanged?.Invoke(null, value);
            }
        }

        public static string ChannelToken = string.Empty;

        public static string BinaryType = "WindowsPlayer";

        public static string RobloxDomain => App.Settings.Prop.RobloxDomain;

        public static bool IsDefaultChannel => Channel.Equals(DefaultChannel, StringComparison.OrdinalIgnoreCase) || Channel.Equals("live", StringComparison.OrdinalIgnoreCase);
        public static bool IsDefaultRobloxDomain => RobloxDomain.Equals(DefaultRobloxDomain, StringComparison.OrdinalIgnoreCase);

        public static string BaseUrl { get; private set; } = null!;

        public static readonly List<HttpStatusCode?> BadChannelCodes = new()
        {
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound
        };

        private static readonly Dictionary<string, ClientVersion> ClientVersionCache = new();

        // a list of roblox deployment locations that we check for, in case one of them don't work
        // these are all weighted based on their priority, so that we pick the most optimal one that we can. 0 = highest
        private static readonly Dictionary<string, int> BaseUrls = new()
        {
            { "https://setup.rbxcdn.com", 0 },
            { "https://setup-aws.rbxcdn.com", 2 },
            { "https://setup-ak.rbxcdn.com", 2 },
            { "https://roblox-setup.cachefly.net", 2 },
            { "https://s3.amazonaws.com/setup.roblox.com", 4 }
        };

        private static async Task<string?> TestConnection(string url, int priority, CancellationToken token)
        {
            string LOG_IDENT = $"Deployment::TestConnection<{url}>";

            await Task.Delay(priority * 1000, token);

            App.Logger.WriteLine(LOG_IDENT, "Connecting...");

            try
            {
                var response = await App.HttpClient.GetAsync($"{url}/versionStudio", token);

                response.EnsureSuccessStatusCode();

                // versionStudio is the version hash for the last MFC studio to be deployed.
                // the response body should always be "version-012732894899482c".
                string content = await response.Content.ReadAsStringAsync(token);

                if (content != VersionStudioHash)
                    throw new InvalidHTTPResponseException($"versionStudio response does not match (expected \"{VersionStudioHash}\", got \"{content}\")");
            }
            catch (TaskCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Connectivity test cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }

            return url;
        }

        /// <summary>
        /// This function serves double duty as the setup mirror enumerator, and as our connectivity check.
        /// Returns null for success.
        /// </summary>
        /// <returns></returns>
        public static async Task<Exception?> InitializeConnectivity()
        {
            const string LOG_IDENT = "Deployment::InitializeConnectivity";

            var tokenSource = new CancellationTokenSource();

            var exceptions = new List<Exception>();
            var tasks = (from entry in BaseUrls select TestConnection(entry.Key, entry.Value, tokenSource.Token)).ToList();

            App.Logger.WriteLine(LOG_IDENT, "Testing connectivity...");

            while (tasks.Any() && String.IsNullOrEmpty(BaseUrl))
            {
                var finishedTask = await Task.WhenAny(tasks);

                tasks.Remove(finishedTask);

                if (finishedTask.IsFaulted)
                    exceptions.Add(finishedTask.Exception!.InnerException!);
                else if (!finishedTask.IsCanceled)
                    BaseUrl = finishedTask.Result;
            }

            // stop other running connectivity tests
            tokenSource.Cancel();

            if (string.IsNullOrEmpty(BaseUrl))
            {
                if (exceptions.Any())
                    return exceptions[0];

                // task cancellation exceptions don't get added to the list
                return new TaskCanceledException("All connection attempts timed out.");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Got {BaseUrl} as the optimal base URL");

            return null;
        }

        public static string GetLocation(string resource)
        {
            string location = BaseUrl;

            if (!IsDefaultChannel)
                location += "/channel/common";

            location += resource;

            return location;
        }

        public async static Task<UserChannel?> GetUserChannel(string binaryType)
        {
            const string LOG_IDENT = "Deployment::GetUserChannel";
            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("clientsettings", "v2/user-channel?binaryType=" + binaryType);
                HttpResponseMessage response = await App.Cookies.AuthGet(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                UserChannel channelInfo = JsonSerializer.Deserialize<UserChannel>(content)!;

                return channelInfo;
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get user channel");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public static async Task<bool> IsChannelPrivate(string channel)
        {
            if (channel == "production")
                channel = "live";

            if (channel == "live")
                return false;

            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("clientsettingscdn", "v2/client-version/WindowsPlayer/channel/" + channel);
                var response = await App.HttpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                if (BadChannelCodes.Contains(ex.StatusCode))
                    return true;
            }

            return false;
        }

        public static async Task<DateTime?> GetVersionTimestamp(string version, CancellationToken token = default)
        {
            const string LOG_IDENT = "Deployment::GetVersionTimestamp";
            const string header = "last-modified";

            // since we arent getting the timestamp during launch there shouldnt be any collisions
            if (string.IsNullOrEmpty(BaseUrl))
                await InitializeConnectivity();

            try
            {
                string location = GetLocation($"/{NormalizeVersionGuid(version)}-rbxPkgManifest.txt");
                var response = await App.HttpClient.GetAsync(location, token);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.TryGetValues(header, out var values))
                {
                    string lastModified = values.First();
                    DateTime dateTime = DateTime.Parse(lastModified);

                    return dateTime;
                }
            } 
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get timestamp for {version}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

/// <summary>
        /// Checks whether one specific published version is still downloadable.
        /// GetInfo can't answer this - it only reports whatever the channel currently
        /// points at, which is never an older version we're trying to roll back to.
        /// </summary>
        public static async Task<bool> IsVersionAvailable(string versionGuid)
        {
            return (await ProbeVersionAsync(versionGuid)).State == VersionAvailability.Available;
        }

        /// <summary>
        /// Probes a single published version and reports what the probe established.
        ///
        /// A transport failure (timeout, DNS, 5xx) deliberately resolves to
        /// <see cref="VersionAvailability.Unknown"/> rather than
        /// <see cref="VersionAvailability.Unavailable"/>. Only an authoritative
        /// 403/404 from the CDN means Roblox actually withdrew the version; treating
        /// a flaky connection as a withdrawal would delete a perfectly good version
        /// from the user's catalogue.
        /// </summary>
        public static async Task<VersionProbeResult> ProbeVersionAsync(string versionGuid, CancellationToken token = default)
        {
            const string LOG_IDENT = "Deployment::ProbeVersionAsync";

            if (String.IsNullOrEmpty(versionGuid))
                return new(VersionAvailability.Unavailable, null);

            versionGuid = NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(BaseUrl))
                await InitializeConnectivity();

            if (String.IsNullOrEmpty(BaseUrl))
                return new(VersionAvailability.Unknown, null);

            try
            {
                string location = GetLocation($"/{versionGuid}-rbxPkgManifest.txt");
                App.Logger.WriteLine(LOG_IDENT, $"Probing {versionGuid}");

                using var request = new HttpRequestMessage(HttpMethod.Head, location);
                using var response = await App.HttpClient.SendAsync(request, token);

                // 403 is what the CDN returns for a version that is no longer
                // published; 404 shows up on mirrors that are behind. Both mean
                // "not downloadable", and neither is a network fault.
                if (response.IsSuccessStatusCode)
                {
                    DateTime? lastModified = null;

                    if (response.Content.Headers.TryGetValues("last-modified", out var values))
                    {
                        if (DateTime.TryParse(values.First(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                            lastModified = parsed;
                    }

                    return new(VersionAvailability.Available, lastModified);
                }

                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
                    return new(VersionAvailability.Unavailable, null);

                App.Logger.WriteLine(LOG_IDENT, $"Probe of {versionGuid} returned {response.StatusCode}, treating as unknown");

                return new(VersionAvailability.Unknown, null);
            }
            catch (TaskCanceledException)
            {
                // a timeout is not evidence of anything about the version itself
                return new(VersionAvailability.Unknown, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to probe {versionGuid}");
                App.Logger.WriteException(LOG_IDENT, ex);

                return new(VersionAvailability.Unknown, null);
            }
        }

        /// <summary>
        /// Downloads and parses a version's package manifest, returning null when it
        /// is not published.
        ///
        /// Used before an install so a withdrawn version fails fast and loudly
        /// instead of half-downloading into a folder that can never work.
        /// </summary>
        public static async Task<PackageManifest?> GetPackageManifestAsync(string versionGuid, CancellationToken token = default)
        {
            const string LOG_IDENT = "Deployment::GetPackageManifestAsync";

            versionGuid = NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(BaseUrl))
                await InitializeConnectivity();

            if (String.IsNullOrEmpty(BaseUrl))
                return null;

            try
            {
                string location = GetLocation($"/{versionGuid}-rbxPkgManifest.txt");
                string data = await App.HttpClient.GetStringAsync(location, token);

                return new PackageManifest(data);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not fetch the package manifest for {versionGuid}");
                App.Logger.WriteException(LOG_IDENT, ex);

                return null;
            }
        }

        /// <summary>
        /// Accepts both spellings of a client version upload guid.
        ///
        /// Roblox's own APIs and the CDN disagree about the "version-" prefix - the
        /// deployment API returns it, and the stock bootstrapper command line often
        /// omits it - so both forms have to resolve to one canonical value or the
        /// same version silently appears twice in a list keyed by guid.
        /// </summary>
        public static string NormalizeVersionGuid(string versionGuid)
        {
            if (String.IsNullOrWhiteSpace(versionGuid))
                return String.Empty;

            versionGuid = versionGuid.Trim();

            // Strip any existing prefix (in any casing) before re-adding the
            // canonical one, so "VERSION-abc" and "abc" produce byte-identical
            // results and cannot end up as two entries in a guid-keyed map.
            if (versionGuid.StartsWith("version-", StringComparison.OrdinalIgnoreCase))
                versionGuid = versionGuid["version-".Length..];

            return String.IsNullOrEmpty(versionGuid)
                ? String.Empty
                : $"version-{versionGuid}";
        }

        /// <summary>
        /// Enumerates every Roblox version Rainstrap can legitimately discover.
        ///
        /// This is NOT an exhaustive historical catalogue, and the returned
        /// <see cref="VersionCatalogResult.IsExhaustive"/> says so. Roblox only
        /// publishes the current version of each channel through its public
        /// deployment API - there is no endpoint that lists prior releases. Older
        /// versions therefore enter the catalogue only through sources that observed
        /// them: the channel's own deployment info, this machine's install history,
        /// and version folders already on disk. Anything else would have to be
        /// invented, which is why this method returns a result object carrying an
        /// explicit exhaustiveness flag rather than a bare list.
        /// </summary>
        public static async Task<VersionCatalogResult> DiscoverVersionsAsync(
            string binaryType,
            IEnumerable<string> observedVersionGuids,
            ISet<string> installedVersionGuids,
            CancellationToken token = default)
        {
            const string LOG_IDENT = "Deployment::DiscoverVersionsAsync";

            string previousBinaryType = BinaryType;
            BinaryType = binaryType;

            try
            {
                var discovered = new Dictionary<string, VersionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                var failures = new List<string>();
                bool latestResolved = false;

                // Source 1: the channel's current deployment. This is the only
                // version Roblox will tell us about directly, and it is what makes
                // "latest official" a real distinction in the UI.
                try
                {
                    ClientVersion current = await GetInfo(Channel, includeTimestamp: true, token: token);

                    if (!String.IsNullOrEmpty(current.VersionGuid))
                    {
                        latestResolved = true;

                        var guid = NormalizeVersionGuid(current.VersionGuid);

                        discovered[guid] = new VersionCatalogEntry
                        {
                            VersionGuid = guid,
                            Version = current.Version,
                            Channel = Channel,
                            Availability = VersionAvailability.Available,
                            PublishedUtc = current.Timestamp,
                            Source = VersionDiscoverySource.ChannelDeployment,
                            IsLatestOfficial = true,
                        };
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Could not read the current deployment for {binaryType}");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    failures.Add(binaryType);
                }

                // Source 2 and 3: versions this machine has actually seen. A guid in
                // local history is a candidate, never proof of availability, so
                // each one is verified against the CDN below before it can be
                // offered for download.
                foreach (string rawGuid in observedVersionGuids)
                {
                    if (String.IsNullOrWhiteSpace(rawGuid))
                        continue;

                    string guid = NormalizeVersionGuid(rawGuid);

                    if (String.IsNullOrEmpty(guid) || discovered.ContainsKey(guid))
                        continue;

                    discovered[guid] = new VersionCatalogEntry
                    {
                        VersionGuid = guid,
                        Version = String.Empty,
                        Channel = String.Empty,
                        Availability = VersionAvailability.Unknown,
                        Source = VersionDiscoverySource.ObservedLocally,
                        IsInstalled = installedVersionGuids.Contains(guid),
                    };
                }

                // Verify every candidate that is not already known-good from the
                // channel. Serialised rather than parallel: a handful of HEAD
                // requests is cheap, and a fan-out is exactly the thing that gets
                // a client rate limited by the CDN.
                foreach (var entry in discovered.Values.Where(x => x.Availability != VersionAvailability.Available))
                {
                    token.ThrowIfCancellationRequested();

                    var probe = await ProbeVersionAsync(entry.VersionGuid, token);

                    entry.Availability = probe.State;

                    if (probe.State == VersionAvailability.Available)
                        entry.PublishedUtc ??= probe.LastModifiedUtc;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Discovered {discovered.Count} version(s), latest resolved: {latestResolved}");

                return new VersionCatalogResult
                {
                    Entries = discovered.Values.ToList(),
                    Failures = failures,
                    LatestResolved = latestResolved,

                    // Never exhaustive: Roblox publishes only the current version per
                    // channel, so a complete historical list does not exist to fetch.
                    IsExhaustive = false,
                };
            }
            finally
            {
                BinaryType = previousBinaryType;
            }
        }

        public static async Task<ClientVersion> GetInfo(string? channel = null, bool behindProductionCheck = false, bool includeTimestamp = false, CancellationToken token = default)
        {
            const string LOG_IDENT = "Deployment::GetInfo";

            if (String.IsNullOrEmpty(channel))
                channel = Channel;

            bool isDefaultChannel = String.Compare(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase) == 0;

            App.Logger.WriteLine(LOG_IDENT, $"Getting deploy info for channel {channel}");

            string cacheKey = $"{channel}-{BinaryType}";

            HttpRequestMessage request = new() 
            {
                Method = HttpMethod.Get
            };
            
            if (!string.IsNullOrEmpty(ChannelToken))
            {
                App.Logger.WriteLine(LOG_IDENT, "Got Roblox-Channel-Token");
                request.Headers.Add("Roblox-Channel-Token", ChannelToken);
            }

            ClientVersion clientVersion;

            if (ClientVersionCache.ContainsKey(cacheKey))
            {
                App.Logger.WriteLine(LOG_IDENT, "Deploy information is cached");
                clientVersion = ClientVersionCache[cacheKey];
            }
            else
            {
                string path = $"v2/client-version/{BinaryType}";

                if (!isDefaultChannel)
                    path += $"/channel/{channel}";

                try
                {
                    request.RequestUri = UrlBuilder.BuildApiUrl("clientsettingscdn", path);
                    clientVersion = await Http.SendJson<ClientVersion>(request, token);
                }
                catch (HttpRequestException httpEx) 
                    when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                {
                    throw new InvalidChannelException(httpEx.StatusCode);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to contact clientsettingscdn! Falling back to clientsettings...");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    try
                    {
                        request.RequestUri = UrlBuilder.BuildApiUrl("clientsettings", path);
                        clientVersion = await Http.SendJson<ClientVersion>(request, token);
                    }
                    catch (HttpRequestException httpEx)
                        when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                    {
                        throw new InvalidChannelException(httpEx.StatusCode);
                    }
                }

                if (clientVersion is null)
                    throw new HttpRequestException($"The deployment API returned no version for {DescribeTarget(BinaryType, channel)}.");

                // check if channel is behind LIVE
                if (!isDefaultChannel && behindProductionCheck)
                {
                    var defaultClientVersion = await GetInfo(DefaultChannel, token: token);

                    if (Utilities.CompareVersions(clientVersion.Version, defaultClientVersion.Version) == VersionComparison.LessThan)
                        clientVersion.IsBehindDefaultChannel = true;
                }
                else
                    clientVersion.IsBehindDefaultChannel = false;

                if (includeTimestamp && clientVersion.Timestamp is null)
                    clientVersion.Timestamp = await GetVersionTimestamp(clientVersion.VersionGuid, token);

                ClientVersionCache[cacheKey] = clientVersion;
            }

            return clientVersion;
        }

        private static string DescribeTarget(string binaryType, string channel)
            => String.IsNullOrEmpty(channel) ? binaryType : $"{binaryType}/{channel}";
    }
}
