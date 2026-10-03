using Bloxstrap.UI.ViewModels.Settings;

using Xunit;

namespace Bloxstrap.Tests
{
    /// <summary>
    /// Version-history classification.
    ///
    /// The history list is a record of what this device installed, and each entry
    /// is badged relative to the installed and newest versions. Getting the
    /// classification wrong mislabels entries, so the rules are pinned here.
    ///
    /// The rollback relaunch arguments this file used to cover are gone: rollback
    /// was replaced by Version Control, which resolves a selection on the next
    /// launch instead of relaunching with a version argument. There is no longer a
    /// second argument-building path to keep in step with the real one.
    /// </summary>
    public class VersionHistoryClassificationTests
    {        [Fact]
        public void TheNewestPublishedVersionIsNotSelectable()
        {
            Assert.False(VersionHistoryRules.IsSelectableInHistory("v2", "v1", "v2"));
        }

        [Fact]
        public void AnOlderVersionIsSelectable()
        {
            Assert.True(VersionHistoryRules.IsSelectableInHistory("v1", "v2", "v3"));
        }

        [Fact]
        public void WithNothingKnownAnOlderVersionIsStillSelectable()
        {
            // Nothing has been measured, so nothing disqualifies the entry.
            Assert.True(VersionHistoryRules.IsSelectableInHistory("v1", null, null));
        }

        [Fact]
        public void ComparisonIsOrdinalAndCaseSensitive()
        {
            // Version guids are opaque strings; a case-insensitive match would
            // classify two genuinely different versions as the same.
            Assert.Equal(
                VersionHistoryRules.VersionState.Previous,
                VersionHistoryRules.Classify("v1", "V1", null));
        }
    }
}