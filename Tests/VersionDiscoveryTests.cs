using Bloxstrap.Models.Entities;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Version discovery: normalisation, deduplication, ordering, and the
    /// exhaustiveness claim.
    /// </summary>
    public class VersionDiscoveryTests
    {
        // ---------- guid normalisation ----------

        [Theory]
        [InlineData("version-02c37bc51a384b8f", "version-02c37bc51a384b8f")]
        [InlineData("02c37bc51a384b8f", "version-02c37bc51a384b8f")]
        [InlineData("VERSION-02c37bc51a384b8f", "version-02c37bc51a384b8f")]
        [InlineData("  02c37bc51a384b8f  ", "version-02c37bc51a384b8f")]
        public void NormalizeVersionGuid_ProducesOneCanonicalSpelling(string input, string expected)
        {
            // Roblox's API and its CDN disagree about the "version-" prefix. If
            // both spellings survived, the same version would appear twice.
            Assert.Equal(expected, Deployment.NormalizeVersionGuid(input));
        }

        [Fact]
        public void NormalizeVersionGuid_IsIdempotent()
        {
            string once = Deployment.NormalizeVersionGuid("02c37bc51a384b8f");
            string twice = Deployment.NormalizeVersionGuid(once);

            Assert.Equal(once, twice);
        }

        [Fact]
        public void NormalizeVersionGuid_HandlesEmpty()
        {
            Assert.Equal(string.Empty, Deployment.NormalizeVersionGuid(string.Empty));
            Assert.Equal(string.Empty, Deployment.NormalizeVersionGuid(null!));
        }

        // ---------- deduplication ----------

        [Fact]
        public void Catalog_DeduplicatesTheSameVersionAcrossSpellings()
        {
            var result = new VersionCatalogResult
            {
                Entries =
                {
                    new VersionCatalogEntry { VersionGuid = "version-02c37bc51a384b8f", Version = "0.741.0.7411058" },
                    // Same version, missing prefix - the same row twice.
                    new VersionCatalogEntry { VersionGuid = "02c37bc51a384b8f", Version = "0.741.0.7411058" },
                    // Case variation must also collapse.
                    new VersionCatalogEntry { VersionGuid = "VERSION-02C37BC51A384B8F", Version = "0.741.0.7411058" },
                },
            };

            var deduped = Dedupe(result.Entries);

            Assert.Single(deduped);
            Assert.Equal("version-02c37bc51a384b8f", deduped[0].VersionGuid);
        }

        [Fact]
        public void Catalog_DeduplicationKeepsTheRicherRecord()
        {
            var entries = new List<VersionCatalogEntry>
            {
                new() { VersionGuid = "version-aaa", Version = string.Empty, Source = VersionDiscoverySource.ObservedLocally },
                new() { VersionGuid = "aaa", Version = "0.600.0.6001", Source = VersionDiscoverySource.ChannelDeployment, IsLatestOfficial = true },
            };

            var deduped = Dedupe(entries);

            Assert.Single(deduped);

            // The channel record knows the version number and the channel, so it
            // must win over the bare locally-observed placeholder.
            Assert.Equal("0.600.0.6001", deduped[0].Version);
            Assert.True(deduped[0].IsLatestOfficial);
            Assert.Equal(VersionDiscoverySource.ChannelDeployment, deduped[0].Source);
        }

        // ---------- ordering ----------

        [Fact]
        public void Catalog_OrdersByVersionMetadataNotByString()
        {
            var result = new VersionCatalogResult
            {
                Entries =
                {
                    new VersionCatalogEntry { VersionGuid = "version-c", Version = "0.9.0.9001" },
                    new VersionCatalogEntry { VersionGuid = "version-a", Version = "0.10.0.10002" },
                    new VersionCatalogEntry { VersionGuid = "version-b", Version = "0.600.0.6001" },
                },
            };

            var sorted = result.SortedEntries();

            // A plain string sort would put 0.9 above 0.10, because "9" > "1".
            // Numeric comparison of the parsed versions must not.
            Assert.Equal("0.600.0.6001", sorted[0].Version);
            Assert.Equal("0.10.0.10002", sorted[1].Version);
            Assert.Equal("0.9.0.9001", sorted[2].Version);
        }

        [Fact]
        public void Catalog_SortsUnparseableVersionsLastRatherThanGuessing()
        {
            var result = new VersionCatalogResult
            {
                Entries =
                {
                    new VersionCatalogEntry { VersionGuid = "version-broken", Version = "not-a-version" },
                    new VersionCatalogEntry { VersionGuid = "version-empty", Version = string.Empty },
                    new VersionCatalogEntry { VersionGuid = "version-good", Version = "0.500.0.5001" },
                },
            };

            var sorted = result.SortedEntries();

            Assert.Equal("0.500.0.5001", sorted[0].Version);
            Assert.Equal("version-broken", sorted[1].VersionGuid);
            Assert.Equal("version-empty", sorted[2].VersionGuid);
        }

        [Fact]
        public void Catalog_OrdersSelectedAndInstalledVersionsFirstAmongEquals()
        {
            // Two records with the same (unknown) version. The one the user
            // actually uses should surface first rather than being buried.
            var result = new VersionCatalogResult
            {
                Entries =
                {
                    new VersionCatalogEntry { VersionGuid = "version-other" },
                    new VersionCatalogEntry { VersionGuid = "version-selected", IsSelected = true },
                },
            };

            Assert.Equal("version-selected", result.SortedEntries()[0].VersionGuid);
        }

        // ---------- exhaustiveness ----------

        [Fact]
        public void Catalog_IsNeverReportedAsExhaustive()
        {
            // Roblox publishes no endpoint that lists historical versions, so a
            // catalogue claiming completeness would be lying to the user.
            var result = new VersionCatalogResult { Entries = { new VersionCatalogEntry { VersionGuid = "version-a" } } };

            Assert.False(result.IsExhaustive);
        }

        [Fact]
        public void Catalog_MissingLatestIsNotClaimedAsResolved()
        {
            var result = new VersionCatalogResult { LatestResolved = false };

            // When the newest release could not be read, the catalogue must not
            // pretend it knows what the latest version is.
            Assert.False(result.LatestResolved);
            Assert.DoesNotContain(result.Entries, x => x.IsLatestOfficial);
        }

        // ---------- availability ----------

        [Fact]
        public void Availability_IsNotTreatedAsAvailableMerelyBecauseAGuidExists()
        {
            // Being present in local history is a candidate, not a guarantee.
            // Until the CDN confirms it, it must read as unknown.
            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-old",
                Version = "0.500.0.5001",
                Source = VersionDiscoverySource.ObservedLocally,
                Availability = VersionAvailability.Unknown,
            };

            Assert.False(entry.CanDownload);
            Assert.NotEqual(VersionAvailability.Available, entry.Availability);
        }

        [Fact]
        public void Availability_AvailableAndNotInstalledMeansDownloadable()
        {
            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-x",
                Availability = VersionAvailability.Available,
                IsInstalled = false,
            };

            Assert.True(entry.CanDownload);
        }

        [Fact]
        public void Availability_InstalledVersionIsNotOfferedForDownload()
        {
            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-x",
                Availability = VersionAvailability.Available,
                IsInstalled = true,
            };

            Assert.False(entry.CanDownload);
        }

        private static List<VersionCatalogEntry> Dedupe(IEnumerable<VersionCatalogEntry> entries)
        {
            var result = new VersionCatalogResult { Entries = entries.ToList() };

            // Mirrors the comparer used when discovery builds its map: canonical
            // guid, case-insensitive, richer record wins.
            return result.Entries
                .GroupBy(x => Deployment.NormalizeVersionGuid(x.VersionGuid), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(x => x.Source == VersionDiscoverySource.ChannelDeployment)
                    .ThenByDescending(x => !String.IsNullOrEmpty(x.Version))
                    .First())
                .ToList();
        }
    }
}