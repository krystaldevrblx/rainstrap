namespace Bloxstrap.Enums.FlagPresets
{
    /// <summary>
    /// Grouping used to organise the built-in FastFlag presets on the Presets page.
    /// The string value is only the display name; the lookup key is
    /// <c>FastFlagPresets_Category_&lt;name&gt;</c> in Strings.resx.
    ///
    /// Only categories that actually contain a verified preset are listed. A category is
    /// not kept "for completeness": the allowlist is small enough that padding these out
    /// would mean offering flags Roblox ignores.
    /// </summary>
    public enum FastFlagPresetCategory
    {
        Graphics,
        Compatibility,
        UI
    }
}
