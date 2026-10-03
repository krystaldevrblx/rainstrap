using Bloxstrap.Models;
using Bloxstrap.UI.ViewModels.Settings;

using Xunit;

namespace Bloxstrap.Tests
{
    /// <summary>
    /// The preset catalogue is only useful if every flag in it is one Roblox
    /// actually reads. A flag outside the allowlist is silently ignored by the
    /// client, so a preset containing one would look applied and do nothing.
    /// </summary>
    public class FastFlagPresetCatalogueTests
    {
        [Fact]
        public void CatalogueContainsNoProblems()
        {
            var problems = FastFlagPresetCatalogue.Validate();

            Assert.True(
                problems.Count == 0,
                "preset catalogue is inconsistent with the Roblox allowlist: "
                + string.Join("; ", problems));
        }

        [Fact]
        public void EveryPresetFlagIsAllowlisted()
        {
            var violations = FastFlagPresetCatalogue.ValidateAgainstAllowlist();

            Assert.True(
                violations.Count == 0,
                "these flags are not on Roblox's allowlist: " + string.Join(", ", violations));
        }

        [Fact]
        public void EveryPresetValueIsInRange()
        {
            // A valid flag carrying an out-of-range value is accepted by the client
            // and then ignored, which is a different and subtler failure than an
            // unlisted flag.
            var invalid = FastFlagPresetCatalogue.ValidateValues();

            Assert.True(
                invalid.Count == 0,
                "these preset values are outside the documented range: " + string.Join(", ", invalid));
        }

        [Fact]
        public void PresetIdsAreUnique()
        {
            var ids = FastFlagPresetCatalogue.All.Select(p => p.Id).ToList();

            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void CatalogueIsNotEmpty()
        {
            Assert.NotEmpty(FastFlagPresetCatalogue.All);
        }

        [Fact]
        public void EveryPresetHasANameAndDescription()
        {
            foreach (var preset in FastFlagPresetCatalogue.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(preset.Name), $"{preset.Id} has no name");
                Assert.False(string.IsNullOrWhiteSpace(preset.Description), $"{preset.Id} has no description");
                Assert.False(preset.Flags.Count == 0, $"{preset.Id} sets no flags");
            }
        }

        [Fact]
        public void EveryPresetIsFindableBySearchingIt()
        {
            foreach (var preset in FastFlagPresetCatalogue.All)
            {
                // Presets are named after their effect so someone can type that
                // effect in. If the name alone does not find it, the search box is
                // decoration.
                Assert.True(
                    preset.Matches(preset.Name),
                    $"{preset.Id} is not findable by its own name '{preset.Name}'");
            }
        }

        [Fact]
        public void GreySkyExistsAndUsesOnlyTheSkyFlag()
        {
            var preset = FastFlagPresetCatalogue.GetById("grey-sky");

            Assert.NotNull(preset);
            Assert.True(preset!.Flags.ContainsKey("FFlagDebugSkyGray"));
            Assert.Equal("True", preset.Flags["FFlagDebugSkyGray"]);
            Assert.True(preset.Flags.Count == 1, "Grey Sky should be a single-flag preset");
        }

        [Fact]
        public void GreySkyIsFindableByTheWordsPeopleWouldType()
        {
            var search = FastFlagPresetCatalogue.Search("grey sky").ToList();

            Assert.Contains(search, p => p.Id == "grey-sky");
        }

        [Fact]
        public void SearchAcceptsAlternativeSpellings()
        {
            // American spelling and the reverse word order both have to work, or
            // the search box misses the most obvious search a player would make.
            Assert.Contains(FastFlagPresetCatalogue.Search("gray"), p => p.Id == "grey-sky");
            Assert.Contains(FastFlagPresetCatalogue.Search("sky gray"), p => p.Id == "grey-sky");
        }

        [Fact]
        public void SearchIsCaseInsensitive()
        {
            Assert.Equal(
                FastFlagPresetCatalogue.Search("grass").Count,
                FastFlagPresetCatalogue.Search("GRASS").Count);
        }

        [Fact]
        public void EmptySearchReturnsEverything()
        {
            Assert.Equal(FastFlagPresetCatalogue.All.Count, FastFlagPresetCatalogue.Search("").Count);
            Assert.Equal(FastFlagPresetCatalogue.All.Count, FastFlagPresetCatalogue.Search(null).Count);
        }

        [Fact]
        public void SearchWithNoMatchesReturnsNothing()
        {
            Assert.Empty(FastFlagPresetCatalogue.Search("zzzzznothingmatchesthis"));
        }

        [Fact]
        public void RendererPresetsConflictWithEachOther()
        {
            // The three renderer presets set the same three flags to different
            // values, so applying one after another silently undoes the last. The
            // UI relies on being told.
            var vulkan = FastFlagPresetCatalogue.GetById("renderer-vulkan");

            Assert.NotNull(vulkan);

            var conflicting = FastFlagPresetCatalogue.ConflictingWith(vulkan!)
                .Select(p => p.Id)
                .ToList();

            Assert.Contains("renderer-d3d11", conflicting);
            Assert.Contains("renderer-opengl", conflicting);
        }

        [Fact]
        public void UnrelatedPresetsDoNotConflict()
        {
            var greySky = FastFlagPresetCatalogue.GetById("grey-sky");

            Assert.NotNull(greySky);
            Assert.Empty(FastFlagPresetCatalogue.ConflictingWith(greySky!));
        }

        [Fact]
        public void NoPresetIsADuplicateOfAnother()
        {
            // Two presets setting an identical flag set are the same preset under
            // two names, which is the "padding out the list" failure.
            var all = FastFlagPresetCatalogue.All;

            foreach (var a in all)
            {
                foreach (var b in all.Where(x => x.Id != a.Id))
                {
                    bool identical =
                        a.Flags.Count == b.Flags.Count &&
                        a.Flags.All(kv =>
                            b.Flags.TryGetValue(kv.Key, out var value) && value == kv.Value);

                    Assert.False(identical, $"{a.Id} and {b.Id} are identical presets");
                }
            }
        }

        [Fact]
        public void GetByIdReturnsNullForAnUnknownId()
        {
            Assert.Null(FastFlagPresetCatalogue.GetById("no-such-preset"));
            Assert.Null(FastFlagPresetCatalogue.GetById(""));
        }
    }
}