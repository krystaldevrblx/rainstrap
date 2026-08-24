using Bloxstrap.Enums;

namespace Bloxstrap
{
    public class FastFlagCatalogEntry
    {
        public string FlagName { get; set; } = "";

        public FastFlagCategory Category { get; set; }

        /// <summary>
        /// Resource key (without the "Catalog.Flag." prefix and ".Desc" suffix) for the flag description.
        /// </summary>
        public string DescriptionResourceKey { get; set; } = "";
    }

    /// <summary>
    /// A curated list of well-understood FastFlags for the catalog page.
    ///
    /// Only flags whose behaviour is broadly known are listed here. Descriptions are kept
    /// deliberately conservative: if Roblox changes or removes a flag, the entry is simply
    /// ignored by the client. Flags with dubious or unverifiable effects (particularly
    /// "ping reducer" style flags) are intentionally excluded.
    /// </summary>
    public static class FastFlagCatalog
    {
        public static IReadOnlyList<FastFlagCatalogEntry> Entries { get; } = new List<FastFlagCatalogEntry>
        {
            new() { FlagName = "DFIntTaskSchedulerTargetFps", Category = FastFlagCategory.Performance, DescriptionResourceKey = "DFIntTaskSchedulerTargetFps" },
            new() { FlagName = "FFlagDebugGraphicsPreferD3D11", Category = FastFlagCategory.Rendering, DescriptionResourceKey = "FFlagDebugGraphicsPreferD3D11" },
            new() { FlagName = "FFlagDebugGraphicsPreferVulkan", Category = FastFlagCategory.Rendering, DescriptionResourceKey = "FFlagDebugGraphicsPreferVulkan" },
            new() { FlagName = "FFlagDebugGraphicsDisableDirect3D11", Category = FastFlagCategory.Rendering, DescriptionResourceKey = "FFlagDebugGraphicsDisableDirect3D11" },
            new() { FlagName = "DFIntDebugFRMQualityLevelOverride", Category = FastFlagCategory.Rendering, DescriptionResourceKey = "DFIntDebugFRMQualityLevelOverride" },
            new() { FlagName = "FIntDebugForceMSAASamples", Category = FastFlagCategory.Visuals, DescriptionResourceKey = "FIntDebugForceMSAASamples" },
            // NOTE: FFlagHandleAltEnterFullscreenManually is intentionally excluded -
            // FastFlagManager force-manages it on load, so user edits would be overwritten.
            new() { FlagName = "DFFlagDisableDPIScale", Category = FastFlagCategory.UI, DescriptionResourceKey = "DFFlagDisableDPIScale" },
            new() { FlagName = "DFIntCanHideGuiGroupId", Category = FastFlagCategory.UI, DescriptionResourceKey = "DFIntCanHideGuiGroupId" },
            new() { FlagName = "FFlagUserShowGuiHideToggles", Category = FastFlagCategory.UI, DescriptionResourceKey = "FFlagUserShowGuiHideToggles" },
            new() { FlagName = "FFlagDebugDisplayFPS", Category = FastFlagCategory.Debug, DescriptionResourceKey = "FFlagDebugDisplayFPS" },
            new() { FlagName = "FFlagDebugRenderForceTechnologyVoxel", Category = FastFlagCategory.Visuals, DescriptionResourceKey = "FFlagDebugRenderForceTechnologyVoxel" },
            new() { FlagName = "FFlagDebugForceFutureIsBrightPhase3", Category = FastFlagCategory.Visuals, DescriptionResourceKey = "FFlagDebugForceFutureIsBrightPhase3" },
            new() { FlagName = "FFlagDebugSkyGray", Category = FastFlagCategory.Optimization, DescriptionResourceKey = "FFlagDebugSkyGray" },
            new() { FlagName = "DFIntCSGLevelOfDetailSwitchingDistance", Category = FastFlagCategory.Optimization, DescriptionResourceKey = "DFIntCSGLevelOfDetailSwitchingDistance" },
            new() { FlagName = "DFIntCSGLevelOfDetailSwitchingDistanceL12", Category = FastFlagCategory.Optimization, DescriptionResourceKey = "DFIntCSGLevelOfDetailSwitchingDistanceL12" },
            new() { FlagName = "DFIntCSGLevelOfDetailSwitchingDistanceL23", Category = FastFlagCategory.Optimization, DescriptionResourceKey = "DFIntCSGLevelOfDetailSwitchingDistanceL23" },
            new() { FlagName = "DFIntCSGLevelOfDetailSwitchingDistanceL34", Category = FastFlagCategory.Optimization, DescriptionResourceKey = "DFIntCSGLevelOfDetailSwitchingDistanceL34" },
        };

        /// <summary>
        /// Presets that only touch catalog-listed flags.
        /// </summary>
        public static class Presets
        {
            // Recommended performance: uncapped-ish FPS target, cheaper lighting, gray sky
            public static readonly Dictionary<string, string> Performance = new()
            {
                { "DFIntTaskSchedulerTargetFps", "240" },
                { "FFlagDebugRenderForceTechnologyVoxel", "True" },
                { "FFlagDebugSkyGray", "True" }
            };

            // Low latency: higher FPS target reduces input-to-photon latency on capable hardware
            public static readonly Dictionary<string, string> LowLatency = new()
            {
                { "DFIntTaskSchedulerTargetFps", "360" },
                { "FFlagDebugGraphicsPreferVulkan", "True" }
            };

            // Minimal UI: DPI scaling off + GUI hide toggles available
            public static readonly Dictionary<string, string> UIMinimal = new()
            {
                { "DFFlagDisableDPIScale", "True" },
                { "FFlagUserShowGuiHideToggles", "True" }
            };
        }

        public static string GetCategoryLabel(FastFlagCategory category)
        {
            return category switch
            {
                FastFlagCategory.Performance => Strings.Catalog_Category_Performance,
                FastFlagCategory.FPS => Strings.Catalog_Category_FPS,
                FastFlagCategory.Rendering => Strings.Catalog_Category_Rendering,
                FastFlagCategory.Network => Strings.Catalog_Category_Network,
                FastFlagCategory.UI => Strings.Catalog_Category_UI,
                FastFlagCategory.Visuals => Strings.Catalog_Category_Visuals,
                FastFlagCategory.Optimization => Strings.Catalog_Category_Optimization,
                FastFlagCategory.Debug => Strings.Catalog_Category_Debug,
                _ => ""
            };
        }

        public static string GetDescription(FastFlagCatalogEntry entry)
        {
            // descriptions come from the resource files so they can be localized;
            // fall back to a neutral notice if one is missing
            var property = typeof(Strings).GetProperty($"Catalog_Flag_{entry.DescriptionResourceKey}_Desc");

            return property?.GetValue(null) as string ?? "";
        }
    }
}
