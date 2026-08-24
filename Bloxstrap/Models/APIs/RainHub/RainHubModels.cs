using System.Text.Json.Serialization;

namespace Bloxstrap.Models.APIs.RainHub
{
    // ─── Pairing ────────────────────────────────────────────────────────────────

    public class PairRequest
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

    public class PairResponse
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = "";

        [JsonPropertyName("deviceToken")]
        public string DeviceToken { get; set; } = "";

        [JsonPropertyName("heartbeatIntervalSeconds")]
        public int HeartbeatIntervalSeconds { get; set; } = 60;
    }

    // ─── Heartbeat / sync ───────────────────────────────────────────────────────

    public class HeartbeatRequest
    {
        [JsonPropertyName("robloxRunning")]
        public bool RobloxRunning { get; set; }

        [JsonPropertyName("appVersion")]
        public string? AppVersion { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }
    }

    /// <summary>
    /// A queued configuration action. Only "apply_profile" exists — RainHub can
    /// never send commands, URLs or executables through this channel.
    /// </summary>
    public class PendingAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = "";

        [JsonPropertyName("profileId")]
        public string ProfileId { get; set; } = "";

        [JsonPropertyName("profileName")]
        public string ProfileName { get; set; } = "";

        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = "";

        [JsonPropertyName("versionNumber")]
        public int VersionNumber { get; set; }

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; } = "";

        [JsonPropertyName("stability")]
        public string Stability { get; set; } = "";

        [JsonPropertyName("compatibility")]
        public string Compatibility { get; set; } = "";

        [JsonPropertyName("flags")]
        public Dictionary<string, string> Flags { get; set; } = new();
    }

    public class HeartbeatResponse
    {
        [JsonPropertyName("serverTime")]
        public string ServerTime { get; set; } = "";

        [JsonPropertyName("heartbeatIntervalSeconds")]
        public int HeartbeatIntervalSeconds { get; set; } = 60;

        [JsonPropertyName("pendingAction")]
        public PendingAction? PendingAction { get; set; }

        [JsonPropertyName("lastSyncAt")]
        public string? LastSyncAt { get; set; }
    }

    public class SyncAckRequest
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("profileId")]
        public string ProfileId { get; set; } = "";

        [JsonPropertyName("profileName")]
        public string ProfileName { get; set; } = "";

        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = "";

        [JsonPropertyName("robloxRunning")]
        public bool RobloxRunning { get; set; }
    }
}
