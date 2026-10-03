namespace Bloxstrap.Enums.FlagPresets
{
    /// <summary>
    /// Retired grouping for the built-in FastFlag presets.
    ///
    /// Presets are no longer organised by category. They are named after the specific
    /// effect they produce - "Grey Sky", "No Grass", "Vulkan Renderer" - and found by
    /// searching, because "Graphics" or "Performance" is not a word anyone types when
    /// they want a grey sky, and a category name groups presets together that do the
    /// opposite of each other.
    ///
    /// The enum is kept, marked obsolete, for one reason: the per-page XAML and the
    /// already-translated Strings.resx entries still resolve
    /// <c>FastFlagPresets_Category_*</c> keys, and removing them would leave 32
    /// translation files referencing strings that no longer exist. It has no effect on
    /// which presets exist.
    /// </summary>
    [Obsolete(
        "Presets are named after their visible effect and found by search. " +
        "Categories are no longer used to group them.")]
    public enum FastFlagPresetCategory
    {
        Graphics,
        Compatibility,
        UI
    }
}
