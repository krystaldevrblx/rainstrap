using System;
using System.Collections.Generic;
using System.Linq;

namespace Bloxstrap.Models
{
    /// <summary>
    /// Roblox's official allowlist for locally configurable Fast Flags.
    ///
    /// This is the ONLY authority on whether a flag may be set from
    /// ClientAppSettings.json. The client ignores any flag that is not on this list, so a
    /// flag that is absent here cannot be offered to the user no matter what any other
    /// source claims.
    ///
    /// Source: https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
    ///   ("Allowlist for local client configuration via Fast Flags", posted by Roblox,
    ///   post version 5, checked 2026-09-26. The thread is open to 2026-08-30 with no
    ///   subsequent Roblox staff post changing the list.)
    ///
    /// Roblox states the list is subject to change without notice, so re-check the source
    /// post when auditing presets rather than trusting a copy of it indefinitely.
    /// </summary>
    public static class FastFlagAllowlist
    {
        public const string SourceUrl =
            "https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569";

        /// <summary>Date the list below was last verified against the source post.</summary>
        public const string VerifiedOn = "2026-10-01";

        /// <summary>
        /// The documented value range for every allowlisted flag, as published in the
        /// source post.
        ///
        /// Recorded here so a preset's value can be checked against the flag it sets
        /// rather than trusted. Every entry was cross-checked against the ranges in the
        /// source post on the <see cref="VerifiedOn"/> date; the previous version of the
        /// preset catalogue used a value of 21 for
        /// <c>DFIntDebugFRMQualityLevelOverride</c> on the basis of a Rainstrap slider
        /// range, which is not the flag's own range and is corrected below.
        ///
        /// A flag absent from this table has a documented boolean value only, so the
        /// value must be "True" or "False" and nothing else.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<int>> IntegerRanges { get; }
            = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                // 0-1000 studs
                ["DFIntCSGLevelOfDetailSwitchingDistance"] = new[] { 0, 1000 },
                ["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = new[] { 0, 1000 },
                ["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = new[] { 0, 1000 },
                ["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = new[] { 0, 1000 },
                // 0 lowest, 3 highest
                ["DFIntTextureQualityOverride"] = new[] { 0, 3 },
                // 1, 2 or 4 only - not a continuous range
                ["FIntDebugForceMSAASamples"] = new[] { 1, 4 },
                // 0 lowest, 21 highest
                ["DFIntDebugFRMQualityLevelOverride"] = new[] { 0, 21 },
                // 0-1000 studs
                ["FIntFRMMinGrassDistance"] = new[] { 0, 1000 },
                ["FIntFRMMaxGrassDistance"] = new[] { 0, 1000 },
            };

        /// <summary>
        /// Values for <c>FIntDebugForceMSAASamples</c> that Roblox actually reads. The
        /// flag's range is 1-4, but only 1, 2 and 4 are meaningful sample counts, so a
        /// preset offering 3 would be accepted by the client and do nothing useful.
        /// </summary>
        public static IReadOnlyList<int> AllowedMsaaSamples { get; } = new[] { 1, 2, 4 };

        /// <summary>
        /// Whether a value is one Roblox will accept for a given allowlisted flag.
        ///
        /// Returns true for a flag with no recorded range, which restricts it to
        /// "True"/"False" - the case for every flag not in <see cref="IntegerRanges"/>.
        /// A value this returns false for would be written to
        /// ClientAppSettings.json and then ignored, so it is a build-time error rather
        /// than something to discover by applying a preset.
        /// </summary>
        public static bool IsValidValue(string flag, string value)
        {
            if (IntegerRanges.TryGetValue(flag, out var range))
            {
                if (!int.TryParse(value, out int number))
                    return false;

                if (flag == "FIntDebugForceMSAASamples")
                    return AllowedMsaaSamples.Contains(number);

                return number >= range[0] && number <= range[1];
            }

            return string.Equals(value, "True", StringComparison.Ordinal)
                || string.Equals(value, "False", StringComparison.Ordinal);
        }

        /// <summary>
        /// Every (flag, value) pair in a set of flags that Roblox would not accept.
        /// Should always be empty; anything listed here is a bug in a preset.
        /// </summary>
        public static IReadOnlyList<string> InvalidValues(IEnumerable<KeyValuePair<string, string>> flags)
            => flags
                .Where(kv => !IsValidValue(kv.Key, kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}")
                .ToList();

        /// <summary>
        /// Every flag Roblox currently recognises from ClientAppSettings.json, grouped as
        /// the source post groups them.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            // Geometry
            "DFIntCSGLevelOfDetailSwitchingDistance",
            "DFIntCSGLevelOfDetailSwitchingDistanceL12",
            "DFIntCSGLevelOfDetailSwitchingDistanceL23",
            "DFIntCSGLevelOfDetailSwitchingDistanceL34",

            // Rendering
            "FFlagHandleAltEnterFullscreenManually",
            "DFFlagTextureQualityOverrideEnabled",
            "DFIntTextureQualityOverride",
            "FIntDebugForceMSAASamples",
            "DFFlagDisableDPIScale",
            "FFlagDebugGraphicsPreferD3D11",
            "FFlagDebugSkyGray",
            "DFFlagDebugPauseVoxelizer",
            "DFIntDebugFRMQualityLevelOverride",
            "FIntFRMMaxGrassDistance",
            "FIntFRMMinGrassDistance",
            "FFlagDebugGraphicsPreferVulkan",
            "FFlagDebugGraphicsPreferOpenGL",

            // User Interface
            "FIntGrassMovementReducedMotionFactor",
        };

        private static readonly HashSet<string> Lookup =
            new(All, StringComparer.Ordinal);

        /// <summary>
        /// Roblox's own list is 18 entries. This count is asserted at build time so a
        /// partial edit to the array above cannot quietly shrink what Rainstrap offers.
        /// </summary>
        public const int ExpectedCount = 18;

        /// <summary>
        /// Whether Roblox will honour this flag when it is set locally. Anything else is
        /// ignored by the client, so it must not be exposed as user-configurable.
        /// </summary>
        public static bool IsAllowed(string flag) => Lookup.Contains(flag);

        public static IReadOnlyList<string> NotAllowed(IEnumerable<string> flags)
            => flags.Where(f => !IsAllowed(f)).Distinct(StringComparer.Ordinal).ToList();
    }
}
