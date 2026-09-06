using System.Text.Json.Serialization;

namespace Bloxstrap.Plugins
{
    public class PluginCatalog
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("plugins")]
        public List<PluginCatalogEntry> Plugins { get; set; } = new();
    }
}
