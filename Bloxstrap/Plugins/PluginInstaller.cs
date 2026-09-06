using System.IO.Compression;
using System.Text.Json;

namespace Bloxstrap.Plugins
{
    public class PluginInstaller
    {
        const string LOG_IDENT = "PluginInstaller";

        private const int MaxArchiveEntries = 100;
        private const long MaxIndividualFileSize = 100 * 1024 * 1024; // 100 MB
        private const long MaxTotalExtractedSize = 500 * 1024 * 1024; // 500 MB

        private readonly string _pluginsDirectory;

        public PluginInstaller(string pluginsDirectory)
        {
            _pluginsDirectory = pluginsDirectory;
            Directory.CreateDirectory(_pluginsDirectory);
        }

        public PluginManifest? InstallPlugin(string packagePath)
        {
            string? tempDir = null;
            try
            {
                if (!File.Exists(packagePath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package not found: {packagePath}");
                    return null;
                }

                if (!packagePath.EndsWith(".rspkg", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Unknown package format: {packagePath}");
                    return null;
                }

                tempDir = Path.Combine(Path.GetTempPath(), "Rainstrap", "PluginInstall", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var validationResult = ValidateAndExtractPackage(packagePath, tempDir);
                if (validationResult is null)
                    return null;

                var (manifest, extractedPaths) = validationResult.Value;

                string pluginDir = Path.Combine(_pluginsDirectory, manifest.Id);
                bool existed = Directory.Exists(pluginDir);

                if (existed)
                {
                    string backupDir = pluginDir + ".bak";

                    try
                    {
                        if (Directory.Exists(backupDir))
                            SafeDeleteDirectory(backupDir);

                        CopyDirectory(pluginDir, backupDir);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to backup existing plugin {manifest.Id}, aborting installation");
                        SafeDeleteDirectory(tempDir);
                        return null;
                    }

                    try
                    {
                        SafeDeleteDirectory(pluginDir);
                        CopyDirectory(tempDir, pluginDir);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to replace plugin {manifest.Id}, restoring backup");

                        SafeDeleteDirectory(pluginDir);

                        try
                        {
                            CopyDirectory(backupDir, pluginDir);
                            App.Logger.WriteLine(LOG_IDENT, $"Restored previous version of plugin: {manifest.Id}");
                        }
                        catch (Exception restoreEx)
                        {
                            App.Logger.WriteException(LOG_IDENT, restoreEx);
                            App.Logger.WriteLine(LOG_IDENT, $"CRITICAL: Failed to restore plugin {manifest.Id} from backup");
                        }

                        SafeDeleteDirectory(tempDir);
                        SafeDeleteDirectory(backupDir);
                        return null;
                    }

                    SafeDeleteDirectory(backupDir);
                }
                else
                {
                    try
                    {
                        CopyDirectory(tempDir, pluginDir);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to install plugin {manifest.Id}");
                        SafeDeleteDirectory(pluginDir);
                        SafeDeleteDirectory(tempDir);
                        return null;
                    }
                }

                App.Logger.WriteLine(LOG_IDENT, $"Installed plugin: {manifest.Id} v{manifest.Version}");
                SafeDeleteDirectory(tempDir);
                return manifest;
            }
            catch (InvalidDataException ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                App.Logger.WriteLine(LOG_IDENT, "Malformed or corrupt archive");
                if (tempDir is not null)
                    SafeDeleteDirectory(tempDir);
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                if (tempDir is not null)
                    SafeDeleteDirectory(tempDir);
                return null;
            }
        }

        public bool UpdatePlugin(string pluginId, string packagePath)
        {
            string? tempDir = null;
            string? backupDir = null;
            string pluginDir = Path.Combine(_pluginsDirectory, pluginId);

            try
            {
                if (!File.Exists(packagePath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package not found: {packagePath}");
                    return false;
                }

                if (!packagePath.EndsWith(".rspkg", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Unknown package format: {packagePath}");
                    return false;
                }

                tempDir = Path.Combine(Path.GetTempPath(), "Rainstrap", "PluginInstall", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var validationResult = ValidateAndExtractPackage(packagePath, tempDir);
                if (validationResult is null)
                    return false;

                var (manifest, extractedPaths) = validationResult.Value;

                if (manifest.Id != pluginId)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package ID mismatch: expected {pluginId}, got {manifest.Id}");
                    SafeDeleteDirectory(tempDir);
                    return false;
                }

                if (!Directory.Exists(pluginDir))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Plugin {pluginId} not found for update");
                    SafeDeleteDirectory(tempDir);
                    return false;
                }

                backupDir = pluginDir + ".bak";

                try
                {
                    if (Directory.Exists(backupDir))
                        SafeDeleteDirectory(backupDir);

                    CopyDirectory(pluginDir, backupDir);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to backup plugin {pluginId}, aborting update");
                    SafeDeleteDirectory(tempDir);
                    SafeDeleteDirectory(backupDir);
                    return false;
                }

                try
                {
                    SafeDeleteDirectory(pluginDir);
                    CopyDirectory(tempDir, pluginDir);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to replace plugin {pluginId}, restoring backup");

                    SafeDeleteDirectory(pluginDir);

                    try
                    {
                        CopyDirectory(backupDir, pluginDir);
                        App.Logger.WriteLine(LOG_IDENT, $"Restored previous version of plugin: {pluginId}");
                    }
                    catch (Exception restoreEx)
                    {
                        App.Logger.WriteException(LOG_IDENT, restoreEx);
                        App.Logger.WriteLine(LOG_IDENT, $"CRITICAL: Failed to restore plugin {pluginId} from backup");
                    }

                    SafeDeleteDirectory(tempDir);
                    SafeDeleteDirectory(backupDir);
                    return false;
                }

                SafeDeleteDirectory(backupDir);
                App.Logger.WriteLine(LOG_IDENT, $"Updated plugin: {pluginId} v{manifest.Version}");
                SafeDeleteDirectory(tempDir);
                return true;
            }
            catch (InvalidDataException ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                App.Logger.WriteLine(LOG_IDENT, "Malformed or corrupt archive");
                SafeDeleteDirectory(tempDir);
                if (backupDir is not null)
                    SafeDeleteDirectory(backupDir);
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                SafeDeleteDirectory(tempDir);
                if (backupDir is not null)
                    SafeDeleteDirectory(backupDir);
                return false;
            }
        }

        public bool UninstallPlugin(string pluginId)
        {
            try
            {
                string pluginDir = Path.Combine(_pluginsDirectory, pluginId);

                if (!Directory.Exists(pluginDir))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Plugin {pluginId} not found for uninstall");
                    return false;
                }

                SafeDeleteDirectory(pluginDir);

                App.Logger.WriteLine(LOG_IDENT, $"Uninstalled plugin: {pluginId}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private (PluginManifest Manifest, HashSet<string> ExtractedPaths)? ValidateAndExtractPackage(string packagePath, string tempDir)
        {
            const string LOG_IDENT = "PluginInstaller::ValidateAndExtractPackage";

            using var archive = ZipFile.OpenRead(packagePath);

            if (archive.Entries.Count > MaxArchiveEntries)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Package contains {archive.Entries.Count} entries, exceeds limit of {MaxArchiveEntries}");
                return null;
            }

            var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry? manifestEntry = null;
            long totalSize = 0;
            var entriesToExtract = new List<(ZipArchiveEntry Entry, string ResolvedPath)>();

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) && string.IsNullOrEmpty(entry.FullName))
                    continue;

                if (!TryNormalizeAndValidateEntry(entry, tempDir, out string? resolvedPath, out string? rejectReason))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Rejected zip entry: {entry.FullName} ({rejectReason})");
                    return null;
                }

                if (entry.Length > MaxIndividualFileSize)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Entry {entry.FullName} size {entry.Length} exceeds limit of {MaxIndividualFileSize}");
                    return null;
                }

                totalSize += entry.Length;
                if (totalSize > MaxTotalExtractedSize)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Total extracted size {totalSize} exceeds limit of {MaxTotalExtractedSize}");
                    return null;
                }

                if (!seenDestinations.Add(resolvedPath!))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Duplicate destination path: {resolvedPath}");
                    return null;
                }

                string entryDir = Path.GetDirectoryName(resolvedPath!) ?? "";
                string entryFileName = Path.GetFileName(resolvedPath!);

                if (entryFileName == "manifest.json" && string.Equals(entryDir, tempDir, StringComparison.OrdinalIgnoreCase))
                    manifestEntry = entry;

                entriesToExtract.Add((entry, resolvedPath!));
            }

            if (manifestEntry is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Package missing manifest.json");
                return null;
            }

            string manifestJson;
            using (var stream = manifestEntry!.Open())
            using (var reader = new StreamReader(stream))
            {
                manifestJson = reader.ReadToEnd();
            }

            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson);

            if (manifest is null || string.IsNullOrEmpty(manifest.Id))
            {
                App.Logger.WriteLine(LOG_IDENT, "Invalid manifest in package");
                return null;
            }

            if (!ValidateManifest(manifest))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Manifest validation failed for {manifest.Id}");
                return null;
            }

            string expectedDll = $"{manifest.Id}.dll";
            bool hasDll = entriesToExtract.Any(e =>
                Path.GetFileName(e.ResolvedPath) == expectedDll &&
                string.Equals(Path.GetDirectoryName(e.ResolvedPath), tempDir, StringComparison.OrdinalIgnoreCase));

            if (!hasDll)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Package missing required DLL: {expectedDll}");
                return null;
            }

            foreach (var (entry, resolvedPath) in entriesToExtract)
            {
                string? dir = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(resolvedPath, overwrite: true);
            }

            var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, resolvedPath) in entriesToExtract)
                extractedPaths.Add(resolvedPath);

            return (manifest, extractedPaths);
        }

        private static bool TryNormalizeAndValidateEntry(ZipArchiveEntry entry, string targetDir, out string? resolvedPath, out string? rejectReason)
        {
            resolvedPath = null;
            rejectReason = null;

            string rawName = entry.FullName;

            if (string.IsNullOrEmpty(rawName))
            {
                rejectReason = "Empty entry name";
                return false;
            }

            string normalized = rawName.Replace('\\', '/');

            if (normalized.StartsWith('/') || normalized.StartsWith("./"))
            {
                rejectReason = "Absolute or relative-rooted path";
                return false;
            }

            if (normalized.Length >= 2 && normalized[1] == ':')
            {
                rejectReason = "Drive-letter path";
                return false;
            }

            try
            {
                resolvedPath = Path.GetFullPath(Path.Combine(targetDir, normalized));
            }
            catch
            {
                rejectReason = "Path could not be resolved";
                return false;
            }

            string fullTargetDir = Path.GetFullPath(targetDir);

            if (!resolvedPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                rejectReason = "Path escapes target directory";
                return false;
            }

            if (resolvedPath.Length > fullTargetDir.Length)
            {
                char charAfterPrefix = resolvedPath[fullTargetDir.Length];

                if (charAfterPrefix != Path.DirectorySeparatorChar && charAfterPrefix != Path.AltDirectorySeparatorChar)
                {
                    rejectReason = "Path is not a proper descendant of target directory";
                    return false;
                }
            }
            else if (resolvedPath.Length == fullTargetDir.Length)
            {
                // entry resolves to the target directory itself — allowed for directories
            }

            return true;
        }

        private bool ValidateManifest(PluginManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.Id) || string.IsNullOrEmpty(manifest.Name))
                return false;

            if (manifest.Id.Contains('/') || manifest.Id.Contains('\\') || manifest.Id.Contains(".."))
                return false;

            if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.Id, @"^[a-zA-Z0-9._-]+$"))
                return false;

            if (!Version.TryParse(manifest.Version, out _))
                return false;

            if (!string.IsNullOrEmpty(manifest.MinRainstrapVersion) && !Version.TryParse(manifest.MinRainstrapVersion, out _))
                return false;

            return true;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        public static bool VerifyPackageHash(string packagePath, string expectedSha256)
        {
            try
            {
                if (!File.Exists(packagePath))
                    return false;

                byte[] fileBytes = File.ReadAllBytes(packagePath);
                string computedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileBytes))
                    .ToLowerInvariant();

                string expected = expectedSha256.ToLowerInvariant().Replace(" ", "");

                return computedHash == expected;
            }
            catch
            {
                return false;
            }
        }

        private static void SafeDeleteDirectory(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
