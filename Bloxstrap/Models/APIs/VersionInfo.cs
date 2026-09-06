using System.Text.Json.Serialization;

namespace Bloxstrap.Models.APIs
{
    public class VersionInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
    }
}
