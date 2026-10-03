using Bloxstrap.Models.Persistable;
using Bloxstrap.UI.ViewModels.Settings;

using System.Text.Json;

using Xunit;

namespace Bloxstrap.Tests
{
    /// <summary>
    /// Update-check persistence.
    ///
    /// The behaviour under test: a successful check survives closing the tab and
    /// restarting Rainstrap, and a failed check does not destroy the last good
    /// result. Both matter because the previous behaviour showed "not checked"
    /// after every restart, and a naive fix (clearing on failure) would trade that
    /// for a worse problem.
    /// </summary>
    public class UpdateCheckPersistenceTests
    {
        private static State.LastUpdateCheckResult Record(
            string? channel = "production",
            string? version = "0.657.0.6570",
            DateTime? checkedUtc = null,
            string? versionGuid = "version-abc123",
            DateTime? publishedUtc = null,
            long latencyMs = 412)
            => new()
            {
                Channel = channel,
                Version = version,
                VersionGuid = versionGuid,
                PublishedUtc = publishedUtc,
                CheckedUtc = checkedUtc ?? new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc),
                LatencyMs = latencyMs,
            };

        // ── Restore ─────────────────────────────────────────────────────────

        [Fact]
        public void NothingIsRestoredWhenThereIsNoSavedRecord()
        {
            var (outcome, restored) = UpdateCheckPersistence.Evaluate(null, "production");

            Assert.Equal(RestoredCheckOutcome.None, outcome);
            Assert.Null(restored);
        }

        [Fact]
        public void ASavedResultForTheCurrentChannelIsRestored()
        {
            var (outcome, restored) = UpdateCheckPersistence.Evaluate(Record(), "production");

            Assert.Equal(RestoredCheckOutcome.Restored, outcome);
            Assert.NotNull(restored);
            Assert.Equal("0.657.0.6570", restored!.Version);
        }

        [Fact]
        public void ARecordWithoutAVersionIsNotAResult()
        {
            // A version number is the entire point of the record.
            var (outcome, restored) = UpdateCheckPersistence.Evaluate(Record(version: null), "production");

            Assert.Equal(RestoredCheckOutcome.None, outcome);
            Assert.Null(restored);
        }

        [Fact]
        public void ARecordWithABlankVersionIsNotAResult()
        {
            var (outcome, _) = UpdateCheckPersistence.Evaluate(Record(version: "   "), "production");

            Assert.Equal(RestoredCheckOutcome.None, outcome);
        }

        [Fact]
        public void ARecordWithNoTimestampIsNotAResult()
        {
            // Without a check time the UI cannot say how old the answer is, which
            // is one of the things the restored state exists to communicate.
            var (outcome, _) = UpdateCheckPersistence.Evaluate(
                Record(checkedUtc: DateTime.MinValue), "production");

            Assert.Equal(RestoredCheckOutcome.None, outcome);
        }

        [Fact]
        public void AResultForADifferentChannelIsNotRestored()
        {
            // A "production" version number says nothing about the channel the user
            // is actually on.
            var (outcome, restored) = UpdateCheckPersistence.Evaluate(Record(channel: "production"), "ptb");

            Assert.Equal(RestoredCheckOutcome.WrongChannel, outcome);
            Assert.Null(restored);
        }

        [Fact]
        public void ChannelComparisonIsCaseInsensitive()
        {
            var (outcome, _) = UpdateCheckPersistence.Evaluate(Record(channel: "PRODUCTION"), "production");

            Assert.Equal(RestoredCheckOutcome.Restored, outcome);
        }

        [Fact]
        public void ARecordWithNoChannelIsAccepted()
        {
            // Older files have no channel recorded; nothing to contradict.
            var (outcome, _) = UpdateCheckPersistence.Evaluate(Record(channel: null), "production");

            Assert.Equal(RestoredCheckOutcome.Restored, outcome);
        }

        // ── Persist ─────────────────────────────────────────────────────────

        [Fact]
        public void ASuccessfulCheckWithAVersionIsPersisted()
        {
            Assert.True(UpdateCheckPersistence.ShouldPersist(succeeded: true, version: "0.657.0.6570"));
        }

        [Fact]
        public void AFailedCheckIsNeverPersisted()
        {
            // Persisting a failure would overwrite the last good result with nothing,
            // which is the failure this mechanism exists to prevent.
            Assert.False(UpdateCheckPersistence.ShouldPersist(succeeded: false, version: "0.657.0.6570"));
        }

        [Fact]
        public void ASuccessfulCheckWithNoVersionIsNotPersisted()
        {
            Assert.False(UpdateCheckPersistence.ShouldPersist(succeeded: true, version: null));
            Assert.False(UpdateCheckPersistence.ShouldPersist(succeeded: true, version: "  "));
        }

        [Fact]
        public void BuildCarriesEveryFieldTheUINeeds()
        {
            var checkedUtc = new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc);
            var publishedUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            var record = UpdateCheckPersistence.Build(
                channel: "production",
                version: "0.657.0.6570",
                versionGuid: "version-abc123",
                publishedUtc: publishedUtc,
                checkedUtc: checkedUtc,
                latencyMs: 412);

            Assert.Equal("production", record.Channel);
            Assert.Equal("0.657.0.6570", record.Version);
            Assert.Equal("version-abc123", record.VersionGuid);
            Assert.Equal(publishedUtc, record.PublishedUtc);
            Assert.Equal(checkedUtc, record.CheckedUtc);
            Assert.Equal(412, record.LatencyMs);
            Assert.True(record.IsValid);
        }

        // ── Serialisation: the file the restart actually reads ─────────────

        [Fact]
        public void ARestoredRecordSurvivesAJsonRoundTrip()
        {
            // This is the real mechanism: State.json is written on save and read on
            // the next launch.
            var original = UpdateCheckPersistence.Build(
                "production", "0.657.0.6570", "version-abc123",
                new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc),
                412);

            var state = new State { LastUpdateCheck = original };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var restoredState = JsonSerializer.Deserialize<State>(json);

            var (outcome, restored) = UpdateCheckPersistence.Evaluate(
                restoredState!.LastUpdateCheck, "production");

            Assert.Equal(RestoredCheckOutcome.Restored, outcome);
            Assert.Equal("0.657.0.6570", restored!.Version);
            Assert.Equal("version-abc123", restored.VersionGuid);
            Assert.Equal(412, restored.LatencyMs);
        }

        [Fact]
        public void AnOlderStateFileWithoutTheRecordStillLoads()
        {
            // A file written before this feature existed must not break startup.
            var legacy = "{\"TestModeWarningShown\":false,\"ForceReinstall\":false}";

            var state = JsonSerializer.Deserialize<State>(legacy);

            Assert.NotNull(state);
            Assert.Null(state!.LastUpdateCheck);

            var (outcome, _) = UpdateCheckPersistence.Evaluate(state.LastUpdateCheck, "production");
            Assert.Equal(RestoredCheckOutcome.None, outcome);
        }

        [Fact]
        public void ACorruptedRecordDoesNotPreventStartup()
        {
            var json = "{\"LastUpdateCheck\":{\"Version\":12345}}";

            // Deserialising a wrong-typed field throws inside System.Text.Json, which
            // is caught by JsonManager and results in a backup + defaults - so what
            // matters is that it does not crash and does not yield a usable result.
            try
            {
                var state = JsonSerializer.Deserialize<State>(json);
                var (_, restored) = UpdateCheckPersistence.Evaluate(state?.LastUpdateCheck, "production");
                Assert.Null(restored);
            }
            catch (JsonException)
            {
                // Also acceptable: the loader's own recovery path handles this.
            }
        }
    }
}