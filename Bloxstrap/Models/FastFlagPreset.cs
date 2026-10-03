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
            IReadOnlyDictionary<string, string> flags,
            FastFlagPresetRisk risk = FastFlagPresetRisk.None,
            string? warning = null,
            IReadOnlyList<string>? searchTerms = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Flags = flags;
            Risk = risk;
            Warning = warning;
            SearchTerms = searchTerms ?? Array.Empty<string>();
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        /// <summary>
        /// Every flag this preset owns, with the value it sets. Values are either
        /// booleans or values Rainstrap's own FastFlag controls already use.
        /// </summary>
        public IReadOnlyDictionary<string, string> Flags { get; }

        public FastFlagPresetRisk Risk { get; }

        /// <summary>Extra guidance shown before applying. Null for low-risk presets.</summary>
        public string? Warning { get; }

        /// <summary>
        /// The words people actually search for this tweak by.
        ///
        /// Presets are named after the effect a player wants ("Grey Sky"), not after a
        /// category they fall into, so the alternative words someone would plausibly
        /// type matter. Kept as plain data so the page can offer a search box, which is
        /// what makes a growing list of specific presets usable.
        /// </summary>
        public IReadOnlyList<string> SearchTerms { get; }

        public int FlagCount => Flags.Count;

        /// <summary>Which of this preset's flags another preset also touches.</summary>
        public IEnumerable<string> OverlappingFlags(FastFlagPreset other)
            => Flags.Keys.Intersect(other.Flags.Keys, StringComparer.Ordinal);

        /// <summary>
        /// Whether this preset matches a free-text query, by name, description or search
        /// term. Case and spacing are ignored, and every word must match something, so
        /// "grey sky" finds "Grey Sky" and "sky gray" finds it too.
        /// </summary>
        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            var haystack = string.Join(" ",
                new[] { Id, Name, Description }
                    .Concat(SearchTerms));

            return query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// The built-in preset catalogue.
    ///
    /// ── Why this list is short ────────────────────────────────────────────────
    ///
    /// Roblox restricts locally configurable FastFlags to a small official allowlist
    /// (see <see cref="FastFlagAllowlist"/>). Anything not on it is silently ignored
    /// by the client, so a preset built from a non-allowlisted flag would look like
    /// it worked and change nothing. That is why this catalogue is a dozen entries
    /// rather than the long list of tweaks floating around online, most of which set
    /// flags Roblox no longer reads.
    ///
    /// ── Why they are named after effects ─────────────────────────────────────
    ///
    /// An earlier version grouped presets into Graphics / Compatibility / UI
    /// categories with names like "Balanced quality" and "Maximum quality". Those
    /// names describe a spectrum rather than a thing a player wants, so nothing in
    /// the list could be found by searching for the effect you were after. Every
    /// preset here is named for the specific visible result it produces - "Grey Sky",
    /// "No Grass", "Flat Textures" - and says which flags it sets.
    ///
    /// ── Rules this catalogue follows ──────────────────────────────────────────
    ///
    ///  * Only officially allowlisted flags are exposed. Checked, not assumed - see
    ///    <see cref="ValidateAgainstAllowlist"/>.
    ///  * Every value is either a boolean or a documented integer range for that
    ///    flag. No value is guessed: the ranges are recorded in
    ///    <see cref="FastFlagAllowlist"/> from the source post.
    ///  * No flag is included to pad out a preset.
    ///  * Presets that genuinely fight each other (the renderer backends) are kept
    ///    separate and reported as overlapping rather than silently merged.
    /// </summary>
    public static class FastFlagPresetCatalogue
    {
        public static IReadOnlyList<FastFlagPreset> All { get; } = new List<FastFlagPreset>
        {
            // ── Visual ────────────────────────────────────────────────────────

            new FastFlagPreset(
                "grey-sky",
                "Grey Sky",
                "Replaces the skybox with flat grey and removes the atmospheric stars. One flag, no other visual change.",
                new Dictionary<string, string>
                {
                    ["FFlagDebugSkyGray"] = "True",
                },
                FastFlagPresetRisk.None,
                searchTerms: new[] { "gray", "skybox", "grey", "no stars", "flat sky", "boring sky" }),

            new FastFlagPreset(
                "no-grass",
                "No Grass",
                "Stops terrain grass rendering entirely. Common in older and very low-end games where grass costs frames for little gain.",
                new Dictionary<string, string>
                {
                    ["FIntFRMMinGrassDistance"] = "0",
                    ["FIntFRMMaxGrassDistance"] = "0",
                },
                FastFlagPresetRisk.None,
                searchTerms: new[] { "grass", "remove grass", "delete grass", "no trees", "performance" }),

            new FastFlagPreset(
                "flat-textures",
                "Flat Textures",
                "Drops texture quality to its lowest level. Textures look noticeably worse, and it is one of the largest frame rate wins available.",
                new Dictionary<string, string>
                {
                    ["DFFlagTextureQualityOverrideEnabled"] = "True",
                    ["DFIntTextureQualityOverride"] = "0",
                },
                FastFlagPresetRisk.Experimental,
                "Textures will look blurry or square in places. Rainstrap's own controls do not cover this flag, so remove the preset to get Roblox's normal textures back.",
                searchTerms: new[] { "potato", "low quality", "no textures", "ugly", "laggy", "performance", "fps" }),

            new FastFlagPreset(
                "reduced-grass-motion",
                "Calm Grass",
                "Stops terrain grass from swaying. Helps readability in games where moving grass hides what is in front of you, and reduces motion.",
                new Dictionary<string, string>
                {
                    ["FIntGrassMovementReducedMotionFactor"] = "True",
                },
                FastFlagPresetRisk.None,
                searchTerms: new[] { "grass", "motion", "accessibility", "no sway", "calm", "still" }),

            // ── Performance ────────────────────────────────────────────────────

            new FastFlagPreset(
                "lowest-graphics",
                "Lowest Graphics",
                "Forces the lowest graphics quality level and switches anti-aliasing off. The go-to preset for low-end hardware.",
                new Dictionary<string, string>
                {
                    ["DFIntDebugFRMQualityLevelOverride"] = "0",
                    ["FIntDebugForceMSAASamples"] = "1",
                },
                FastFlagPresetRisk.None,
                searchTerms: new[] { "potato", "performance", "fps", "laggy", "slow", "low end", "old pc", "no aa" }),

            new FastFlagPreset(
                "no-antialiasing",
                "No Anti-Aliasing",
                "Turns MSAA off. Edges look noticeably jagged, but it is a consistent small frame rate gain.",
                new Dictionary<string, string>
                {
                    ["FIntDebugForceMSAASamples"] = "1",
                },
                FastFlagPresetRisk.None,
                searchTerms: new[] { "msaa", "jagged", "sharp edges", "no aa", "performance", "fps" }),

            new FastFlagPreset(
                "no-voxel-lighting",
                "No Voxel Lighting",
                "Disables voxel-based lighting. Scenes lose some of their soft indirect lighting and gain frames in return.",
                new Dictionary<string, string>
                {
                    ["DFFlagDebugPauseVoxelizer"] = "True",
                },
                FastFlagPresetRisk.Experimental,
                "Lighting looks flatter and some games are built around the voxel lighting look. Rainstrap's own controls do not cover this flag.",
                searchTerms: new[] { "lighting", "voxel", "flat lighting", "performance", "fps" }),

            // ── Compatibility ──────────────────────────────────────────────────

            new FastFlagPreset(
                "renderer-d3d11",
                "Direct3D 11 Renderer",
                "Forces the Direct3D 11 backend and turns the other two off. The most broadly supported option, and the usual fix for a game that renders incorrectly or crashes on launch.",
                new Dictionary<string, string>
                {
                    ["FFlagDebugGraphicsPreferD3D11"] = "True",
                    ["FFlagDebugGraphicsPreferVulkan"] = "False",
                    ["FFlagDebugGraphicsPreferOpenGL"] = "False",
                },
                searchTerms: new[] { "d3d11", "directx", "renderer", "crash", "flickering", "default" }),

            new FastFlagPreset(
                "renderer-vulkan",
                "Vulkan Renderer",
                "Forces the Vulkan backend. Can fix frame pacing on some systems, and some games render incorrectly on Direct3D 11.",
                new Dictionary<string, string>
                {
                    ["FFlagDebugGraphicsPreferVulkan"] = "True",
                    ["FFlagDebugGraphicsPreferD3D11"] = "False",
                    ["FFlagDebugGraphicsPreferOpenGL"] = "False",
                },
                FastFlagPresetRisk.Experimental,
                "Vulkan is not available on every system, and where it is, an older driver can be worse than Direct3D 11. If Roblox will not start, apply the Direct3D 11 preset instead.",
                searchTerms: new[] { "vulkan", "renderer", "frame pacing", "stutter", "ticking", "flicker" }),

            new FastFlagPreset(
                "renderer-opengl",
                "OpenGL Renderer",
                "Forces the OpenGL backend. The least commonly supported of the three; mainly useful as a diagnostic to tell a driver problem apart from a game problem.",
                new Dictionary<string, string>
                {
                    ["FFlagDebugGraphicsPreferOpenGL"] = "True",
                    ["FFlagDebugGraphicsPreferD3D11"] = "False",
                    ["FFlagDebugGraphicsPreferVulkan"] = "False",
                },
                FastFlagPresetRisk.Risky,
                "OpenGL is the least supported backend and rendering can be visibly wrong. Use Direct3D 11 unless you have a specific reason.",
                searchTerms: new[] { "opengl", "renderer", "legacy", "diagnostic", "broken graphics" }),

            new FastFlagPreset(
                "fix-dpi-scaling",
                "Fix HiDPI Scaling",
                "Stops Windows display scaling from being applied to the Roblox window. The usual fix for a blurry or wrongly-sized window on a high-DPI display.",
                new Dictionary<string, string>
                {
                    ["DFFlagDisableDPIScale"] = "True",
                },
                searchTerms: new[] { "dpi", "blurry", "hidpi", "4k", "scaling", "tiny window", "giant window", "sharp" }),

            new FastFlagPreset(
                "manual-fullscreen",
                "Fix Alt+Enter Fullscreen",
                "Returns Alt+Enter fullscreen toggling to manual handling. For setups where the game goes borderless or fails to restore from fullscreen.",
                new Dictionary<string, string>
                {
                    ["FFlagHandleAltEnterFullscreenManually"] = "False",
                },
                searchTerms: new[] { "alt enter", "fullscreen", "borderless", "alt-tab", "windowed" }),

            new FastFlagPreset(
                "no-csg-detail",
                "No CSG Detail Switching",
                "Sets the CSG level-of-detail switching distances to zero, so large CSG models are not swapped for lower detail versions. Removes visible pop-in on builds made from many parts.",
                new Dictionary<string, string>
                {
                    ["DFIntCSGLevelOfDetailSwitchingDistance"] = "0",
                    ["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "0",
                    ["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "0",
                    ["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "0",
                },
                FastFlagPresetRisk.Experimental,
                "Affects every CSG model in every game, including ones that rely on detail swapping to stay performant. Rainstrap's own Mesh LOD controls set these flags too.",
                searchTerms: new[] { "csg", "pop in", "detail", "lod", "blocks", "builds", "flickering models" }),
        };

        static FastFlagPresetCatalogue()
        {
            // Fail loudly in debug builds if the catalogue ever drifts from Roblox's
            // published allowlist, rather than shipping a preset that silently does
            // nothing. Covers a non-allowlisted flag, an out-of-range value, a
            // duplicated id, and a partially edited allowlist.
            var problems = Validate();
            if (problems.Count > 0)
                Debug.Assert(false, "FastFlag preset catalogue is inconsistent with the "
                    + "Roblox allowlist: " + string.Join("; ", problems));
        }

        /// <summary>
        /// Flags used by the catalogue that Roblox does not allow to be configured
        /// locally. Should always be empty; anything listed here is a bug.
        /// </summary>
        public static IReadOnlyList<string> ValidateAgainstAllowlist()
            => FastFlagAllowlist.NotAllowed(All.SelectMany(p => p.Flags.Keys));

        /// <summary>
        /// Every (flag, value) pair in the catalogue that Roblox would not accept,
        /// prefixed by the preset that owns it. Should always be empty.
        ///
        /// Catches a different class of mistake from
        /// <see cref="ValidateAgainstAllowlist"/>: a perfectly valid flag carrying a
        /// value outside its documented range. The client accepts the flag and then
        /// ignores the setting, so a preset could look applied and do nothing.
        /// </summary>
        public static IReadOnlyList<string> ValidateValues()
            => All
                .SelectMany(p => FastFlagAllowlist
                    .InvalidValues(p.Flags)
                    .Select(bad => $"{p.Id}: {bad}"))
                .ToList();

        /// <summary>
        /// Checks every documented constraint at once: allowlisted flags, valid values,
        /// the expected allowlist size, and unique preset ids. Returns each problem
        /// found; an empty result means the catalogue matches Roblox's published list.
        /// </summary>
        public static IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (FastFlagAllowlist.All.Count != FastFlagAllowlist.ExpectedCount)
                problems.Add(
                    $"allowlist has {FastFlagAllowlist.All.Count} entries, expected " +
                    $"{FastFlagAllowlist.ExpectedCount} - re-check the Roblox source post");

            problems.AddRange(ValidateAgainstAllowlist().Select(f => $"not allowlisted: {f}"));
            problems.AddRange(ValidateValues());

            foreach (var id in All.GroupBy(p => p.Id, StringComparer.Ordinal)
                         .Where(g => g.Count() > 1)
                         .Select(g => g.Key))
                problems.Add($"duplicate preset id: {id}");

            return problems;
        }

        public static FastFlagPreset? GetById(string id)
            => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

        /// <summary>
        /// Presets matching a free-text query, in catalogue order. An empty query
        /// returns everything, so the unfiltered list and a search share one path.
        /// </summary>
        public static IReadOnlyList<FastFlagPreset> Search(string? query)
            => string.IsNullOrWhiteSpace(query)
                ? All
                : All.Where(p => p.Matches(query)).ToList();

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
