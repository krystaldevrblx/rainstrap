using System.Text.Json.Serialization;

namespace Bloxstrap.Plugins
{
    public class PluginManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("apiVersion")]
        public string ApiVersion { get; set; } = "1.0";

        [JsonPropertyName("minRainstrapVersion")]
        public string MinRainstrapVersion { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

        [JsonPropertyName("verified")]
        public bool Verified { get; set; } = false;

        [JsonPropertyName("repository")]
        public string? Repository { get; set; } = null;

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; } = null;

        [JsonPropertyName("isOfficial")]
        public bool IsOfficial { get; set; } = false;
    }
}
