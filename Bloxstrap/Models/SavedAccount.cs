namespace Bloxstrap.Models
{
    /// <summary>
    /// Metadata for a Roblox account saved in the Account Manager.
    /// This model must never contain authentication secrets - those are stored
    /// separately, encrypted with DPAPI (see AccountsManager).
    /// </summary>
    public class SavedAccount
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("userId")]
        public long UserId { get; set; } = 0;

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string AccountDisplayName { get; set; } = "";

        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; } = "";

        [JsonPropertyName("addedAt")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastUsedAt")]
        public DateTime? LastUsedAt { get; set; } = null;
    }
}
