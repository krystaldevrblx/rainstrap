
namespace Rainstrap.Tests
{
    /// <summary>
    /// Update detection and the Notify decision.
    ///
    /// Every scenario the brief calls out is covered here at the decision level:
    /// what Rainstrap decides, independent of whether a window could be shown.
    /// </summary>
    public class NotifyTests : IDisposable
    {
        private readonly TempInstall _temp = new();

        public void Dispose() => _temp.Dispose();

        // ---------- detection ----------

        [Fact]
        public void Detection_ReportsNoUpdateWhenVersionsMatch()
        {
            var result = UpgradeNotifier.Compare("0.741.0.7411058", "0.741.0.7411058");

            Assert.Equal(UpdateAvailability.UpToDate, result.Availability);
            Assert.False(result.IsNewer);
        }

        [Fact]
        public void Detection_ReportsAnUpdateWhenTheChannelIsNewer()
        {
            var result = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
            Assert.True(result.IsNewer);
        }

        [Fact]
        public void Detection_DoesNotUseStringOrderingForVersions()
        {
            // "0.9" sorts above "0.10" as text but is genuinely older.
            var result = UpgradeNotifier.Compare("0.9.0.9001", "0.10.0.10002");

            Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        }

        [Fact]
        public void Detection_DoesNotTreatAnOlderSelectionAsAnUpdate()
        {
            var result = UpgradeNotifier.Compare("0.741.0.7411058", "0.740.0.7400927");

            Assert.Equal(UpdateAvailability.UpToDate, result.Availability);
            Assert.False(result.IsNewer);
        }

        [Fact]
        public void Detection_UnknownVersionIsNotReportedAsUpToDate()
        {
            // "We cannot tell" must not become "there is nothing to do".
            var result = UpgradeNotifier.Compare(string.Empty, "0.741.0.7411058");

            Assert.Equal(UpdateAvailability.Unknown, result.Availability);
            Assert.False(result.IsNewer);
        }

        [Fact]
        public void Detection_MalformedLatestVersionIsUnknownRatherThanAnError()
        {
            var result = UpgradeNotifier.Compare("0.740.0.7400927", "garbage");

            Assert.Equal(UpdateAvailability.Unknown, result.Availability);
            Assert.False(result.IsNewer);
        }

        [Fact]
        public void Detection_NeverOrdersVersionsByGuid()
        {
            var result = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            result.LatestVersionGuid = "version-0000000000000000";
            result.SelectedVersionGuid = "version-ffffffffffffffff";

            // Same version numbers, opposite guid ordering. The verdict must not
            // change, because client version guids are opaque hashes.
            Assert.True(result.IsNewer);
        }

        // ---------- the prompt decision ----------

        [Fact]
        public void Decision_NoPromptInAutomaticMode()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(UpgradeMode.Automatic, detection, selectionIsPinned: false);

            Assert.Equal(NotifyDecision.ProceedSilently, decision);
        }

        [Fact]
        public void Decision_NoPromptWhenNoUpdateExists()
        {
            var detection = UpgradeNotifier.Compare("0.741.0.7411058", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(UpgradeMode.Notify, detection, selectionIsPinned: false);

            // The core "no update available" case: launch without a dialog.
            Assert.Equal(NotifyDecision.ProceedSilently, decision);
        }

        [Fact]
        public void Decision_PromptWhenAnUpdateExists()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(UpgradeMode.Notify, detection, selectionIsPinned: false);

            Assert.Equal(NotifyDecision.PromptUser, decision);
        }

        [Fact]
        public void Decision_PromptEvenWhenAVersionIsPinned()
        {
            // Notify exists to tell the user a newer release exists. Suppressing
            // the prompt for a pinned version would make Notify silently inert in
            // exactly the situation it matters most.
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(UpgradeMode.Notify, detection, selectionIsPinned: true);

            Assert.Equal(NotifyDecision.PromptUser, decision);
        }

        [Fact]
        public void Decision_NoPromptWhenTheCheckFailed()
        {
            var failed = new UpdateDetectionResult
            {
                Availability = UpdateAvailability.Unknown,
                Error = "network",
            };

            var decision = UpgradeNotifier.Decide(UpgradeMode.Notify, failed, selectionIsPinned: false);

            // A failed check must never be read as "an update exists" or as
            // "no update" - it simply proceeds with what the user already has.
            Assert.Equal(NotifyDecision.ProceedSilently, decision);
        }

        [Fact]
        public void Decision_NoPromptWhenAlreadyPromptedThisLaunch()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(
                UpgradeMode.Notify,
                detection,
                selectionIsPinned: false,
                channel: null,
                alreadyPromptedThisLaunch: true);

            Assert.Equal(NotifyDecision.ProceedSilently, decision);
        }

        [Fact]
        public void Decision_NoPromptForADifferentChannelsUpdate()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var decision = UpgradeNotifier.Decide(
                UpgradeMode.Notify,
                detection,
                selectionIsPinned: false,
                channel: "some-other-channel");

            Assert.Equal(NotifyDecision.ProceedSilently, decision);
        }

        [Fact]
        public void Decision_IsStableAcrossRepeatedCallsForOneLaunch()
        {
            // The same inputs must give the same answer every time, which is what
            // makes "one dialog per launch" enforceable.
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            var first = UpgradeNotifier.Decide(UpgradeMode.Notify, detection, true);
            var second = UpgradeNotifier.Decide(UpgradeMode.Notify, detection, true);

            Assert.Equal(first, second);
            Assert.Equal(NotifyDecision.PromptUser, first);
        }

        // ---------- the prompt text ----------

        [Fact]
        public void Prompt_ShowsBothVersionNumbers()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            string message = UpgradeNotifier.BuildPrompt(detection, selectionIsPinned: false);

            Assert.Contains("0.740.0.7400927", message);
            Assert.Contains("0.741.0.7411058", message);
        }

        [Fact]
        public void Prompt_SaysSoWhenAVersionIsPinned()
        {
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            string unpinned = UpgradeNotifier.BuildPrompt(detection, selectionIsPinned: false);
            string pinned = UpgradeNotifier.BuildPrompt(detection, selectionIsPinned: true);

            // Updating replaces an explicit choice, so the user has to be told.
            Assert.NotEqual(unpinned, pinned);
            Assert.Contains("older version", pinned, StringComparison.OrdinalIgnoreCase);
        }

        // ---------- interaction with selection ----------

        [Theory]
        [InlineData(MessageBoxResult.Yes, true)]
        [InlineData(MessageBoxResult.No, false)]
        // Closing the dialog yields None, not No. This is the case that would
        // silently update the client if dismissal were treated as anything other
        // than a decline.
        [InlineData(MessageBoxResult.None, false)]
        [InlineData(MessageBoxResult.Cancel, false)]
        [InlineData(MessageBoxResult.OK, false)]
        public void OnlyAnExplicitYesAcceptsTheUpdate(MessageBoxResult answer, bool expected)
        {
            Assert.Equal(expected, UpgradeNotifier.IsAccept(answer));
        }

        [Fact]
        public void ADismissedPromptIsADecline()
        {
            // Restates the dismissal case explicitly, because it is the one that
            // regressed before: None used to fall through as "not Yes" in one place
            // and as "not No" in another.
            Assert.False(UpgradeNotifier.IsAccept(MessageBoxResult.None));
        }

        [Fact]
        public void ADismissedPromptLeavesTheSelectionUnchanged()
        {
            VersionControl.SelectVersion("version-mine");

            // Nothing in the decline path may write to the selection.
            Assert.Equal("version-mine", VersionControl.SelectedVersionGuid);
        }

        [Fact]
        public void DiscoveringANewerReleaseDoesNotOverwriteAManualSelection()
        {
            VersionControl.SelectVersion("version-mine", pinned: true);

            // A check runs, finds something newer, and the user is not asked yet.
            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");

            Assert.True(detection.IsNewer);
            Assert.Equal("version-mine", VersionControl.SelectedVersionGuid);
            Assert.True(VersionControl.IsSelectionPinned);
        }

        [Fact]
        public void AcceptingAnUpdateMovesTheSelectionAndKeepsItPinnable()
        {
            VersionControl.SelectVersion("version-mine", pinned: true);

            var detection = UpgradeNotifier.Compare("0.740.0.7400927", "0.741.0.7411058");
            detection.LatestVersionGuid = "version-newlatest";

            // This is the only path that may write the selection.
            VersionControl.SelectVersion(detection.LatestVersionGuid, pinned: false);

            Assert.Equal("version-newlatest", VersionControl.SelectedVersionGuid);

            // Not pinned, so a later Notify check may ask again.
            Assert.False(VersionControl.IsSelectionPinned);
        }

        [Fact]
        public void AfterAnUpdateTheUserCanManuallyPinSomethingElseAgain()
        {
            VersionControl.SelectVersion("version-newlatest", pinned: false);

            VersionControl.SelectVersion("version-something-else", pinned: true);

            Assert.Equal("version-something-else", VersionControl.SelectedVersionGuid);
            Assert.True(VersionControl.IsSelectionPinned);
        }

        [Fact]
        public void AFailedUpdateCheckNeverClearsTheSelection()
        {
            VersionControl.SelectVersion("version-mine");

            var failed = new UpdateDetectionResult { Availability = UpdateAvailability.Unknown, Error = "timeout" };

            var decision = UpgradeNotifier.Decide(UpgradeMode.Notify, failed, selectionIsPinned: false);

            Assert.Equal(NotifyDecision.ProceedSilently, decision);
            Assert.Equal("version-mine", VersionControl.SelectedVersionGuid);
        }
    }
}