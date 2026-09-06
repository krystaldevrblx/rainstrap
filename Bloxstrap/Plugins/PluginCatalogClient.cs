using System.Text.Json;

namespace Bloxstrap.Plugins
{
    public class PluginCatalogClient
    {
        const string LOG_IDENT = "PluginCatalogClient";

        private const string CatalogueUrl = "https://raw.githubusercontent.com/krystaldevrblx/rainstrap/main/plugin-catalog/plugins.json";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        private readonly string _cachePath;
        private PluginCatalog? _cachedCatalog;
        private DateTime _lastFetchUtc = DateTime.MinValue;

        public PluginCatalogClient(string baseDirectory)
        {
            string cacheDir = Path.Combine(baseDirectory, "Plugins", "cache");
            Directory.CreateDirectory(cacheDir);
            _cachePath = Path.Combine(cacheDir, "catalogue.json");
        }

        public async Task<PluginCatalog?> GetCatalogAsync()
        {
            if (_cachedCatalog is not null && (DateTime.UtcNow - _lastFetchUtc) < CacheDuration)
                return _cachedCatalog;

            var catalog = await FetchCatalogAsync();

            if (catalog is not null)
            {
                _cachedCatalog = catalog;
                _lastFetchUtc = DateTime.UtcNow;
                SaveCache(catalog);
            }
            else
            {
                _cachedCatalog = LoadCache();
            }

            return _cachedCatalog;
        }

        public async Task<bool> DownloadPackageAsync(string url, string destinationPath, string expectedSha256)
        {
            const string LOG_ID = "PluginCatalogClient::DownloadPackage";

            try
            {
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_ID, $"Rejected non-HTTPS package URL: {url}");
                    return false;
                }

                var uri = new Uri(url);
                var response = await App.HttpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();

                string computedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
                    .ToLowerInvariant();

                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    string expected = expectedSha256.ToLowerInvariant().Replace(" ", "");

                    if (computedHash != expected)
                    {
                        App.Logger.WriteLine(LOG_ID, $"SHA-256 mismatch for {url}: expected {expected}, got {computedHash}");
                        return false;
                    }
                }

                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(destinationPath, bytes);

                App.Logger.WriteLine(LOG_ID, $"Downloaded package to {destinationPath} ({bytes.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_ID, ex);
                App.Logger.WriteLine(LOG_ID, $"Failed to download package from {url}");
                return false;
            }
        }

        private async Task<PluginCatalog?> FetchCatalogAsync()
        {
            try
            {
                if (!CatalogueUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Rejected non-HTTPS catalogue URL");
                    return null;
                }

                var uri = new Uri(CatalogueUrl);
                var catalog = await Http.GetJson<PluginCatalog>(uri);

                if (catalog is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Received null catalogue");
                    return null;
                }

                if (catalog.SchemaVersion < 1)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Unsupported catalogue schema version: {catalog.SchemaVersion}");
                    return null;
                }

                if (catalog.Plugins is null)
                    catalog.Plugins = new();

                App.Logger.WriteLine(LOG_IDENT, $"Fetched catalogue with {catalog.Plugins.Count} plugins");
                return catalog;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                App.Logger.WriteLine(LOG_IDENT, "Failed to fetch catalogue from GitHub");
                return null;
            }
        }

        private void SaveCache(PluginCatalog catalog)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = false };
                string json = JsonSerializer.Serialize(catalog, options);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private PluginCatalog? LoadCache()
        {
            try
            {
                if (!File.Exists(_cachePath))
                    return null;

                string json = File.ReadAllText(_cachePath);
                var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

                if (catalog is not null)
                    App.Logger.WriteLine(LOG_IDENT, "Loaded cached catalogue");

                return catalog;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }
    }
}
