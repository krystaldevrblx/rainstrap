using Bloxstrap.Models;

using Xunit;

namespace Bloxstrap.Tests
{
    /// <summary>
    /// Value validation against Roblox's published ranges.
    ///
    /// This is a separate suite from the catalogue tests because it tests the
    /// validator itself. A validator that accepts everything would let the
    /// catalogue tests pass while the guarantee they rely on was false.
    /// </summary>
    public class FastFlagAllowlistTests
    {
        [Fact]
        public void AllowlistHoldsTheDocumentedNumberOfEntries()
        {
            // Roblox's post lists 18. A partial edit to the array must fail loudly
            // rather than quietly shrink what Rainstrap offers.
            Assert.Equal(FastFlagAllowlist.ExpectedCount, FastFlagAllowlist.All.Count);
            Assert.Equal(18, FastFlagAllowlist.All.Count);
        }

        [Fact]
        public void AllowlistHasNoDuplicates()
        {
            Assert.Equal(
                FastFlagAllowlist.All.Count,
                FastFlagAllowlist.All.Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [InlineData("FFlagDebugSkyGray", "True", true)]
        [InlineData("FFlagDebugSkyGray", "False", true)]
        [InlineData("FFlagDebugSkyGray", "true", false)]   // case matters on the wire
        [InlineData("FFlagDebugSkyGray", "1", false)]
        [InlineData("DFIntTextureQualityOverride", "0", true)]
        [InlineData("DFIntTextureQualityOverride", "3", true)]
        [InlineData("DFIntTextureQualityOverride", "4", false)]   // range is 0-3
        [InlineData("DFIntTextureQualityOverride", "-1", false)]
        [InlineData("DFIntDebugFRMQualityLevelOverride", "0", true)]
        [InlineData("DFIntDebugFRMQualityLevelOverride", "21", true)]
        [InlineData("DFIntDebugFRMQualityLevelOverride", "22", false)]
        [InlineData("FIntFRMMinGrassDistance", "0", true)]
        [InlineData("FIntFRMMaxGrassDistance", "1000", true)]
        [InlineData("FIntFRMMaxGrassDistance", "1001", false)]
        [InlineData("FIntDebugForceMSAASamples", "1", true)]
        [InlineData("FIntDebugForceMSAASamples", "2", true)]
        [InlineData("FIntDebugForceMSAASamples", "4", true)]
        [InlineData("FIntDebugForceMSAASamples", "3", false)]   // in range but not a real sample count
        public void ValueValidationMatchesTheDocumentedRange(string flag, string value, bool expected)
        {
            Assert.Equal(expected, FastFlagAllowlist.IsValidValue(flag, value));
        }

        [Theory]
        [InlineData("DFIntTextureQualityOverride", "not a number")]
        [InlineData("DFIntTextureQualityOverride", "")]
        [InlineData("DFIntTextureQualityOverride", "2.5")]
        [InlineData("DFIntTextureQualityOverride", null)]
        public void NonIntegerValuesAreRejected(string flag, string? value)
        {
            Assert.False(FastFlagAllowlist.IsValidValue(flag, value!));
        }

        [Fact]
        public void UnknownFlagsAreOnlyValidAsBooleans()
        {
            // A flag with no recorded integer range is a boolean flag, so anything
            // other than True/False is wrong for it.
            Assert.True(FastFlagAllowlist.IsValidValue("SomeUnlistedFlag", "True"));
            Assert.False(FastFlagAllowlist.IsValidValue("SomeUnlistedFlag", "3"));
            Assert.False(FastFlagAllowlist.IsValidValue("SomeUnlistedFlag", "yes"));
        }

        [Fact]
        public void IsAllowedRejectsFlagsOutsideTheAllowlist()
        {
            // The flag the community most wants and cannot have. If this ever
            // returns true, the list has been edited incorrectly.
            Assert.False(FastFlagAllowlist.IsAllowed("FFlagRenderDebugCheckThreading2"));
            Assert.False(FastFlagAllowlist.IsAllowed("FFlagDisablePostFx"));
            Assert.False(FastFlagAllowlist.IsAllowed("FFlagMovePrerenderV2"));
            Assert.False(FastFlagAllowlist.IsAllowed(""));
            Assert.False(FastFlagAllowlist.IsAllowed("fflagdebugskygray"));  // case sensitive
        }

        [Fact]
        public void IsAllowedAcceptsTheSkyFlag()
        {
            Assert.True(FastFlagAllowlist.IsAllowed("FFlagDebugSkyGray"));
        }

        [Fact]
        public void NotAllowedListsExactlyTheRejectedFlags()
        {
            var rejected = FastFlagAllowlist.NotAllowed(new[]
            {
                "FFlagDebugSkyGray",   // allowed
                "FFlagDisablePostFx",  // not allowed
                "FFlagDebugSkyGray",   // duplicate, allowed
                "FFlagMovePrerender",  // not allowed
            });

            Assert.Equal(
                new[] { "FFlagDisablePostFx", "FFlagMovePrerender" }.OrderBy(x => x),
                rejected.OrderBy(x => x));
        }

        [Fact]
        public void NotAllowedOnAnEmptyListIsEmpty()
        {
            Assert.Empty(FastFlagAllowlist.NotAllowed(Array.Empty<string>()));
        }

        [Fact]
        public void InvalidValuesReportsOnlyTheOffendingPairs()
        {
            var invalid = FastFlagAllowlist.InvalidValues(new Dictionary<string, string>
            {
                ["FFlagDebugSkyGray"] = "True",                  // fine
                ["DFIntTextureQualityOverride"] = "9",           // out of range
                ["FIntFRMMaxGrassDistance"] = "0",               // fine
            });

            Assert.Single(invalid);
            Assert.Contains("DFIntTextureQualityOverride=9", invalid[0]);
        }

        [Fact]
        public void InvalidValuesIsEmptyForAFullyValidSet()
        {
            var invalid = FastFlagAllowlist.InvalidValues(FastFlagPresetCatalogue.All
                .SelectMany(p => p.Flags));

            Assert.Empty(invalid);
        }

        [Fact]
        public void AllowedMsaaSamplesAreTheRealSampleCounts()
        {
            Assert.Equal(new[] { 1, 2, 4 }, FastFlagAllowlist.AllowedMsaaSamples);
        }
    }
}