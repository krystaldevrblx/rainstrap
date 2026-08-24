using System.Text.RegularExpressions;

namespace Bloxstrap.Integrations.RainHub
{
    public class RainHubProfileFlagIssue
    {
        public string Flag = "";
        public string Reason = "";
    }

    /// <summary>
    /// Local validation of FastFlag configurations pushed from RainHub.
    ///
    /// These checks mirror the server-side analyzer, but they run on-device so a
    /// compromised or misbehaving server can never push malformed flag data into
    /// ClientAppSettings.json. Flags are pure configuration — nothing here is or
    /// ever becomes executable.
    /// </summary>
    public static class RainHubProfileValidator
    {
        public const int MaxFlags = 256;
        public const int MaxKeyLength = 128;
        public const int MaxValueLength = 512;

        private static readonly Regex ValidFlagName = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private static readonly string[] KnownPrefixes =
        {
            "FFlag", "DFlag", "SFFlag",
            "FInt", "DFInt", "SFInt",
            "FString", "DFString", "SFString",
            "FLog", "DFLog", "SFLog",
        };

        /// <summary>
        /// Validates the full flag set. Returns a list of blocking issues; empty
        /// means the configuration is safe to apply. Warnings (e.g. unknown
        /// prefixes) are reported but do not block application.
        /// </summary>
        public static (List<RainHubProfileFlagIssue> Blocking, List<RainHubProfileFlagIssue> Warnings) Validate(
            Dictionary<string, string>? flags
        )
        {
            var blocking = new List<RainHubProfileFlagIssue>();
            var warnings = new List<RainHubProfileFlagIssue>();

            if (flags is null)
            {
                blocking.Add(new RainHubProfileFlagIssue { Reason = "Flag data missing" });
                return (blocking, warnings);
            }

            if (flags.Count > MaxFlags)
            {
                blocking.Add(new RainHubProfileFlagIssue { Reason = $"Profile sets {flags.Count} flags, which exceeds the limit of {MaxFlags}" });
                return (blocking, warnings);
            }

            foreach (var pair in flags)
            {
                string name = pair.Key;
                string value = pair.Value ?? "";

                if (string.IsNullOrWhiteSpace(name))
                {
                    blocking.Add(new RainHubProfileFlagIssue { Reason = "Encountered an empty flag name" });
                    continue;
                }

                if (name.Length > MaxKeyLength)
                {
                    blocking.Add(new RainHubProfileFlagIssue { Flag = name, Reason = "Flag name too long" });
                    continue;
                }

                if (!ValidFlagName.IsMatch(name))
                {
                    blocking.Add(new RainHubProfileFlagIssue { Flag = name, Reason = "Flag name contains invalid characters" });
                    continue;
                }

                if (value.Length > MaxValueLength)
                {
                    blocking.Add(new RainHubProfileFlagIssue { Flag = name, Reason = "Flag value too long" });
                    continue;
                }

                string? prefix = GetPrefix(name);

                if (prefix is null)
                {
                    // Not recognized as a standard Roblox flag. Do not block (it may be
                    // newer than our list), but surface it as a warning.
                    warnings.Add(new RainHubProfileFlagIssue { Flag = name, Reason = "Unrecognized flag prefix" });
                    continue;
                }

                bool isBool = prefix == "FFlag" || prefix == "DFlag" || prefix == "SFFlag";
                bool isInt = !isBool && prefix.EndsWith("Int");

                if (isInt && !Regex.IsMatch(value, "^-?[0-9]+$"))
                {
                    blocking.Add(new RainHubProfileFlagIssue { Flag = name, Reason = $"Invalid value '{value}' for integer flag ({prefix})" });
                    continue;
                }

                if (isBool && !(value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase)))
                {
                    blocking.Add(new RainHubProfileFlagIssue { Flag = name, Reason = $"Invalid value '{value}' for boolean flag ({prefix})" });
                    continue;
                }
            }

            return (blocking, warnings);
        }

        private static string? GetPrefix(string name)
        {
            foreach (string prefix in KnownPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return prefix;
            }
            return null;
        }
    }
}
