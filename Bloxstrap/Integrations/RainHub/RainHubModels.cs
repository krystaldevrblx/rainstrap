using System.Text.Json.Serialization;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>
    /// Data contracts for the RainHub public API.
    ///
    /// These mirror RainHub's OpenAPI spec (lib/api-spec/openapi.yaml) exactly. RainHub
    /// is the authoritative source for Roblox data and server selection - Rainstrap does
    /// not reimplement any of it, it only renders what RainHub returns.
    ///
    /// Nullable members are genuinely optional: RainHub omits values it cannot measure
    /// rather than inventing them, and the UI must do the same.
    /// </summary>

    #region Servers

    public sealed class RainHubServer
    {
        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = "";

        [JsonPropertyName("currentPlayers")]
        public int CurrentPlayers { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        /// <summary>0-1 ratio as computed by RainHub.</summary>
        [JsonPropertyName("fullness")]
        public double Fullness { get; set; }

        /// <summary>Null when Roblox did not report a ping for this server.</summary>
        [JsonPropertyName("ping")]
        public int? Ping { get; set; }

        [JsonPropertyName("pingStatus")]
        public string? PingStatus { get; set; }

        /// <summary>Null when Roblox did not report FPS for this server.</summary>
        [JsonPropertyName("fps")]
        public double? Fps { get; set; }
    }

    public sealed class RainHubServerList
    {
        [JsonPropertyName("servers")]
        public List<RainHubServer> Servers { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("placeId")]
        public string PlaceId { get; set; } = "";

        [JsonPropertyName("totalFetched")]
        public int? TotalFetched { get; set; }

        [JsonPropertyName("pageCount")]
        public int? PageCount { get; set; }

        [JsonPropertyName("limitReached")]
        public bool? LimitReached { get; set; }

        [JsonPropertyName("lastUpdated")]
        public long? LastUpdated { get; set; }

        [JsonPropertyName("isPopular")]
        public bool? IsPopular { get; set; }

        /// <summary>
        /// Human readable reason the list is empty, when RainHub could not produce one
        /// (game disabled server browsing, rate limited, not found, ...).
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>Server list filters accepted by RainHub's /api/servers.</summary>
    public enum RainHubServerFilter
    {
        All,
        Empty,
        Low,
        High,
        Richest
    }

    /// <summary>Server list sort modes accepted by RainHub's /api/servers.</summary>
    public enum RainHubServerSort
    {
        Fullness,
        Emptiness,
        Ping
    }

    #endregion

    #region Games

    public sealed class RainHubSearchGame
    {
        [JsonPropertyName("placeId")]
        public string PlaceId { get; set; } = "";

        [JsonPropertyName("universeId")]
        public string UniverseId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int? MaxPlayers { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("creatorName")]
        public string? CreatorName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public sealed class RainHubPlaceInfo
    {
        [JsonPropertyName("placeId")]
        public string PlaceId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("activePlayerCount")]
        public int ActivePlayerCount { get; set; }
    }

    public sealed class RainHubTrendingGame
    {
        [JsonPropertyName("placeId")]
        public string PlaceId { get; set; } = "";

        [JsonPropertyName("universeId")]
        public string UniverseId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("activePlayers")]
        public int ActivePlayers { get; set; }

        [JsonPropertyName("peakPlayers")]
        public int? PeakPlayers { get; set; }

        [JsonPropertyName("visits")]
        public int? Visits { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int? MaxPlayers { get; set; }

        [JsonPropertyName("trend")]
        public string? Trend { get; set; }

        /// <summary>
        /// Null when RainHub has not collected enough history yet. Rainstrap shows
        /// "collecting data" in that case rather than rendering a made-up percentage.
        /// </summary>
        [JsonPropertyName("trendPercent")]
        public double? TrendPercent { get; set; }

        [JsonPropertyName("trendAvailable")]
        public bool? TrendAvailable { get; set; }

        /// <summary>
        /// RainHub's authoritative 0-100 discovery score. Rendered as-is; Rainstrap
        /// never recalculates it.
        /// </summary>
        [JsonPropertyName("discoveryScore")]
        public double? DiscoveryScore { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("rank")]
        public int? Rank { get; set; }
    }

    public sealed class RainHubGenreStat
    {
        [JsonPropertyName("genre")]
        public string Genre { get; set; } = "";

        [JsonPropertyName("popularity")]
        public double Popularity { get; set; }

        [JsonPropertyName("gameCount")]
        public int GameCount { get; set; }
    }

    public sealed class RainHubTrendReport
    {
        [JsonPropertyName("trending")]
        public List<RainHubTrendingGame> Trending { get; set; } = new();

        [JsonPropertyName("rising")]
        public List<RainHubTrendingGame> Rising { get; set; } = new();

        [JsonPropertyName("falling")]
        public List<RainHubTrendingGame> Falling { get; set; } = new();

        [JsonPropertyName("upAndComing")]
        public List<RainHubTrendingGame> UpAndComing { get; set; } = new();

        [JsonPropertyName("genres")]
        public List<RainHubGenreStat> Genres { get; set; } = new();

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("totalPlayers")]
        public int TotalPlayers { get; set; }

        [JsonPropertyName("gameCount")]
        public int? GameCount { get; set; }

        [JsonPropertyName("trackedCount")]
        public int? TrackedCount { get; set; }
    }

    #endregion

    #region Live signals

    public sealed class RainHubLiveSignal
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("subtext")]
        public string? Subtext { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("placeId")]
        public string? PlaceId { get; set; }

        [JsonPropertyName("gameName")]
        public string? GameName { get; set; }

        [JsonPropertyName("urgent")]
        public bool? Urgent { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public sealed class RainHubLiveSignalsResponse
    {
        [JsonPropertyName("signals")]
        public List<RainHubLiveSignal> Signals { get; set; } = new();

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }

    #endregion

    #region Device pairing

    public sealed class RainHubPairRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = "";

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = "";

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = "";

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = "windows";
    }

    public sealed class RainHubPairResponse
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = "";

        /// <summary>
        /// The scoped device credential issued by RainHub. Stored encrypted at rest and
        /// never written to the log.
        /// </summary>
        [JsonPropertyName("deviceToken")]
        public string DeviceToken { get; set; } = "";

        [JsonPropertyName("heartbeatIntervalSeconds")]
        public int HeartbeatIntervalSeconds { get; set; } = 60;
    }

    public sealed class RainHubHeartbeatResponse
    {
        [JsonPropertyName("serverTime")]
        public string? ServerTime { get; set; }

        [JsonPropertyName("heartbeatIntervalSeconds")]
        public int HeartbeatIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Always null now that the RainHub profile platform has been removed. Kept so a
        /// future queued action is not silently dropped.
        /// </summary>
        [JsonPropertyName("pendingAction")]
        public JsonElement? PendingAction { get; set; }

        [JsonPropertyName("lastSyncAt")]
        public string? LastSyncAt { get; set; }
    }

    public sealed class RainHubApiErrorBody
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    #endregion
}
