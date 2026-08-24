using Bloxstrap.Models.APIs.RainHub;

namespace Bloxstrap.Integrations.RainHub
{
    public class RainHubProfileApplyResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = "";
        public List<RainHubProfileFlagIssue> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Applies (and rolls back) FastFlag profiles pushed from RainHub.
    ///
    /// Safety model:
    ///  * the payload is validated locally before anything touches disk;
    ///  * the previous flag set is backed up to Profiles/RainHubBackup.json so a
    ///    bad push can always be rolled back from the settings page;
    ///  * flags are plain configuration data merged into FastFlagManager — there
    ///    is no code execution surface of any kind here.
    /// </summary>
    public static class RainHubProfileApplier
    {
        private static string BackupLocation => Path.Combine(Paths.Base, "Profiles", "RainHubBackup.json");

        public static RainHubProfileApplyResult Apply(PendingAction action)
        {
            const string LOG_IDENT = "RainHubProfileApplier::Apply";

            var result = new RainHubProfileApplyResult();

            if (action.Type != "apply_profile")
            {
                result.Error = $"Unsupported action type '{action.Type}'";
                App.Logger.WriteLine(LOG_IDENT, result.Error);
                return result;
            }

            var (blocking, warnings) = RainHubProfileValidator.Validate(action.Flags);
            result.Warnings = warnings;

            if (blocking.Count > 0)
            {
                result.Error = $"Profile rejected by local validation: {blocking[0].Reason}";
                if (blocking.Count > 1)
                    result.Error += $" (+{blocking.Count - 1} more issues)";

                App.Logger.WriteLine(LOG_IDENT, $"Validation failed for '{action.ProfileName}': {result.Error}");
                return result;
            }

            try
            {
                BackupCurrentFlags();
            }
            catch (Exception ex)
            {
                // A failed backup should not block applying, but it is worth logging loudly.
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            try
            {
                foreach (var pair in action.Flags)
                    App.FastFlags.SetValue(pair.Key, pair.Value);

                App.FastFlags.Save();

                result.Success = true;
                App.Logger.WriteLine(
                    LOG_IDENT,
                    $"Applied '{action.ProfileName}' v{action.VersionNumber} ({action.Flags.Count} flags, stability: {action.Stability})"
                );
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                result.Error = ex.Message;

                // Roll back in-memory state to whatever was loaded before we touched it.
                Rollback(deleteBackupOnSuccess: true);
            }

            return result;
        }

        /// <summary>
        /// Restores the flag backup taken at the last apply. Returns null on
        /// success or an error description.
        /// </summary>
        /// <param name="deleteBackupOnSuccess">
        /// When true (failed auto-apply), the backup is consumed after restore.
        /// User-triggered rollbacks keep the backup so it can be restored again.
        /// </param>
        public static string? Rollback(bool deleteBackupOnSuccess = false)
        {
            const string LOG_IDENT = "RainHubProfileApplier::Rollback";
            bool restored = false;

            try
            {
                if (!File.Exists(BackupLocation))
                    return "No RainHub backup exists";

                Dictionary<string, string>? backup = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(BackupLocation));

                if (backup is null)
                    return "Backup file is empty";

                App.FastFlags.Prop.Clear();
                foreach (var pair in backup)
                    App.FastFlags.SetValue(pair.Key, pair.Value);

                App.FastFlags.Save();

                restored = true;
                App.Logger.WriteLine(LOG_IDENT, $"Restored {backup.Count} flags from backup");
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return ex.Message;
            }
            finally
            {
                if (restored && deleteBackupOnSuccess && File.Exists(BackupLocation))
                {
                    try { File.Delete(BackupLocation); } catch { /* non-fatal */ }
                }
            }
        }

        public static bool HasBackup => File.Exists(BackupLocation);

        private static void BackupCurrentFlags()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupLocation)!);
            string contents = JsonSerializer.Serialize(App.FastFlags.OriginalProp, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupLocation, contents);
        }
    }
}
