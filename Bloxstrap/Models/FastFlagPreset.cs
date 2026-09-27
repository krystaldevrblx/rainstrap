using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Bloxstrap.Enums.FlagPresets;

namespace Bloxstrap.Models
{
    /// <summary>
    /// How risky a preset is, which drives the warning shown before applying it.
    /// </summary>
    public enum FastFlagPresetRisk
    {
        /// <summary>Well understood, no known downside.</summary>
        None,

        /// <summary>Documented but lightly tested; may behave differently per system.</summary>
        Experimental,

        /// <summary>Can cause crashes, rendering or performance problems on some systems.</summary>
        Risky
    }

    /// <summary>
    /// A curated set of FastFlags that together aim for one specific configuration.
    ///
    /// Presets are built-in and maintained in code. There is deliberately no upload,
    /// download, rating or sharing mechanism - this is a local convenience, not a
    /// marketplace.
    /// </summary>
    public class FastFlagPreset
    {
        public FastFlagPreset(
            string id,
            string name,
            string description,
            FastFlagPresetCategory category,
            IReadOnlyDictionary<string, string> flags,
            FastFlagPresetRisk risk = FastFlagPresetRisk.None,
            string? warning = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Flags = flags;
            Risk = risk;
            Warning = warning;
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public FastFlagPresetCategory Category { get; }

        /// <summary>
        /// Every flag this preset owns, with the value it sets. Values are either
        /// booleans or values Rainstrap's own FastFlag controls already use.
        /// </summary>
        public IReadOnlyDictionary<string, string> Flags { get; }

        public FastFlagPresetRisk Risk { get; }

        /// <summary>Extra guidance shown before applying. Null for low-risk presets.</summary>
        public string? Warning { get; }

        public int FlagCount => Flags.Count;

        /// <summary>Which of this preset's flags another preset also touches.</summary>
        public IEnumerable<string> OverlappingFlags(FastFlagPreset other)
            => Flags.Keys.Intersect(other.Flags.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// The built-in preset catalogue.
    ///
    /// Every flag here is on Roblox's official allowlist for local client configuration
    /// (see <see cref="FastFlagAllowlist"/>). The client silently ignores any flag that is
    /// not on that list, so a flag missing from it would do nothing while still appearing
    /// in the settings file and in the user's exported config.
    ///
    /// Rules this catalogue follows:
    ///  * Only officially allowlisted flags are exposed. This is checked, not assumed -
    ///    see <see cref="ValidateAgainstAllowlist"/>.
    ///  * No flag is included to pad out a category. A preset that would be empty or
    ///    meaningless without unofficial flags is removed rather than kept as a
    ///    placeholder, and a category with no presets is dropped.
    ///  * Every value is either a boolean or a value Rainstrap's own FastFlag controls
    ///    already use (MSAA 1/2/4, FRM quality 1-21), so no value is guessed.
    ///  * Presets that genuinely fight each other (the renderer backends) are kept
    ///    separate and reported as overlapping rather than silently merged.
    /// </summary>
    public static class FastFlagPresetCatalogue
    {
        // Values already used by Rainstrap's own controls, so presets cannot drift away
        // from what the sliders and toggles on the FastFlags page accept.
        private const string FRM_LOW = "4";    // FastFlags page FRM quality slider: 1-21
        private const string FRM_HIGH = "21";  // ...and the default when enabled.
        private const string MSAA_OFF = "1";   // FastFlags page MSAA modes: 1 / 2 / 4
        private const string MSAA_4X = "4";

        public static IReadOnlyList<FastFlagPreset> All { get; } = new List<FastFlagPreset>
        {
            new FastFlagPreset(
                "graphics-balanced",
                "Balanced quality",
                "Lowers the default render quality and turns off MSAA. Usually the biggest frame rate win on older hardware.",
                FastFlagPresetCategory.Graphics,
                new Dictionary<string, string>
                {
                    ["DFIntDebugFRMQualityLevelOverride"] = FRM_LOW,
                    ["FIntDebugForceMSAASamples"] = MSAA_OFF,
                }),

            new FastFlagPreset(
                "graphics-maximum",
                "Maximum quality",
                "Sets the highest render quality level and enables 4x MSAA. Costs frame rate on most systems.",
                FastFlagPresetCategory.Graphics,
                new Dictionary<string, string>
                {
                    ["DFIntDebugFRMQualityLevelOverride"] = FRM_HIGH,
                    ["FIntDebugForceMSAASamples"] = MSAA_4X,
                },
                FastFlagPresetRisk.Experimental,
                "Higher quality settings can reduce frame rate, especially with integrated graphics."),

            new FastFlagPreset(
                "compat-d3d11",
                "D3D11 renderer",
                "Forces the Direct3D 11 backend. A common fix for rendering and crash issues on older drivers.",
                FastFlagPresetCategory.Compatibility,
                new Dictionary<string, string>
                {
                    ["FFlagDebugGraphicsPreferD3D11"] = "True",
                    ["FFlagDebugGraphicsPreferVulkan"] = "False",
                    ["FFlagDebugGraphicsPreferOpenGL"] = "False",
                }),

            new FastFlagPreset(
                "compat-vulkan",
                "Vulkan renderer",
                "Forces the Vulkan backend. Can help frame pacing on some systems, but is less widely supported.",
                FastFlagPresetCategory.Compatibility,
                new Dictionary<string, string>
                {
                    ["FFlagDebugGraphicsPreferVulkan"] = "True",
                    ["FFlagDebugGraphicsPreferD3D11"] = "False",
                    ["FFlagDebugGraphicsPreferOpenGL"] = "False",
                },
                FastFlagPresetRisk.Experimental,
                "Vulkan is not available on every system. If Roblox fails to start, apply the D3D11 renderer preset instead."),

            new FastFlagPreset(
                "compat-display-and-fullscreen",
                "Display scaling and fullscreen",
                "Fixes display scaling on high-DPI setups and returns Alt+Enter to manual fullscreen handling.",
                FastFlagPresetCategory.UI,
                new Dictionary<string, string>
                {
                    ["DFFlagDisableDPIScale"] = "True",
                    ["FFlagHandleAltEnterFullscreenManually"] = "False",
                }),
        };

        static FastFlagPresetCatalogue()
        {
            // Fail loudly in debug builds if a non-allowlisted flag is ever added here,
            // rather than shipping a preset that silently does nothing.
            var violations = ValidateAgainstAllowlist();
            if (violations.Count > 0)
                Debug.Assert(false, "FastFlag presets contain flags Roblox does not allow: "
                    + string.Join(", ", violations));
        }

        /// <summary>
        /// Flags used by the catalogue that Roblox does not allow to be configured
        /// locally. Should always be empty; anything listed here is a bug.
        /// </summary>
        public static IReadOnlyList<string> ValidateAgainstAllowlist()
            => FastFlagAllowlist.NotAllowed(All.SelectMany(p => p.Flags.Keys));

        public static FastFlagPreset? GetById(string id)
            => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

        /// <summary>
        /// Presets that set at least one of the same flags to a different value.
        /// Surfaced in the UI so overlapping presets never silently overwrite each other.
        /// </summary>
        public static IReadOnlyList<FastFlagPreset> ConflictingWith(FastFlagPreset preset)
            => All.Where(other =>
                    other.Id != preset.Id &&
                    preset.OverlappingFlags(other).Any(flag =>
                        !other.Flags.TryGetValue(flag, out var value) ||
                        !string.Equals(value, preset.Flags[flag], StringComparison.Ordinal)))
                .ToList();
    }
}
