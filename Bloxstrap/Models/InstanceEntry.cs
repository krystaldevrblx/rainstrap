namespace Bloxstrap.Models
{
    /// <summary>
    /// A Roblox client instance that Rainstrap has launched.
    /// </summary>
    public class InstanceEntry
    {
        [JsonPropertyName("pid")]
        public int Pid { get; set; } = 0;

        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; } = null;

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("startedAtUtc")]
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
