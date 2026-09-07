using System.Text.Json.Serialization;

namespace Bloxstrap.Plugins
{
    public class PluginCatalogEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("apiVersion")]
        public string ApiVersion { get; set; } = "1";

        [JsonPropertyName("minRainstrapVersion")]
        public string? MinRainstrapVersion { get; set; } = null;

        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

        [JsonPropertyName("official")]
        public bool Official { get; set; } = false;

        [JsonPropertyName("verified")]
        public bool Verified { get; set; } = false;

        [JsonPropertyName("packageUrl")]
        public string PackageUrl { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; } = null;

        [JsonPropertyName("category")]
        public string? Category { get; set; } = null;

        [JsonPropertyName("builtin")]
        public bool Builtin { get; set; } = false;
    }
}
