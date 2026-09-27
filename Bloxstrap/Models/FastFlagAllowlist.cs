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
        public const string VerifiedOn = "2026-09-26";

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
        /// Whether Roblox will honour this flag when it is set locally. Anything else is
        /// ignored by the client, so it must not be exposed as user-configurable.
        /// </summary>
        public static bool IsAllowed(string flag) => Lookup.Contains(flag);

        public static IReadOnlyList<string> NotAllowed(IEnumerable<string> flags)
            => flags.Where(f => !IsAllowed(f)).Distinct(StringComparer.Ordinal).ToList();
    }
}
