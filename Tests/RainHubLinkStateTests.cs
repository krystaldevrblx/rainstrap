using Bloxstrap.Integrations.RainHub;
using Bloxstrap.UI.ViewModels.Settings;

using Xunit;

namespace Bloxstrap.Tests
{
    /// <summary>
    /// RainHub account-linking state.
    ///
    /// The decision under test: when the RainHub tab is shown, and when a stored
    /// credential is thrown away. Both are irreversible-ish actions taken on a
    /// user's behalf, so the rules must not be guesswork - particularly the rule
    /// that a transient failure must never unlink a working device.
    /// </summary>
    public class RainHubLinkStateTests
    {
        private const string AToken = "rhb_something_at_all";

        // ── Tab visibility ──────────────────────────────────────────────────

        [Fact]
        public void AUsableTokenMeansLinked()
        {
            Assert.Equal(RainHubLinkState.LinkState.Linked, RainHubLinkState.Evaluate(AToken, false));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NoTokenMeansUnlinked(string? token)
        {
            Assert.Equal(RainHubLinkState.LinkState.Unlinked, RainHubLinkState.Evaluate(token, false));
        }

        [Fact]
        public void ARejectedCredentialAsksForARelink()
        {
            // Distinct from "never linked": the user did link, and RainHub stopped
            // accepting it, so they need to do something rather than find nothing.
            Assert.Equal(RainHubLinkState.LinkState.NeedsRelink, RainHubLinkState.Evaluate(null, true));
        }

        [Fact]
        public void AServerRejectionDoesNotOverrideAUsableToken()
        {
            // A 403 on one call does not mean the stored credential is dead; the
            // account store decides that separately.
            Assert.Equal(RainHubLinkState.LinkState.Linked, RainHubLinkState.Evaluate(AToken, true));
        }

        [Fact]
        public void OnlyLinkedStateShowsTheTab()
        {
            // The tab is a dead end when unlinked, so it must not appear there.
            Assert.Equal(RainHubLinkState.LinkState.Linked, RainHubLinkState.Evaluate(AToken, false));
            Assert.NotEqual(RainHubLinkState.LinkState.Linked, RainHubLinkState.Evaluate(null, false));
            Assert.NotEqual(RainHubLinkState.LinkState.Linked, RainHubLinkState.Evaluate(null, true));
        }

        [Fact]
        public void HasUsableTokenRejectsBlankValues()
        {
            Assert.True(RainHubLinkState.HasUsableToken(AToken));
            Assert.False(RainHubLinkState.HasUsableToken(null));
            Assert.False(RainHubLinkState.HasUsableToken(""));
        }

        // ── When a credential is discarded ──────────────────────────────────

        [Theory]
        [InlineData(RainHubError.NotLinked)]
        [InlineData(RainHubError.Forbidden)]
        public void RejectedCredentialsAreDiscarded(RainHubError error)
        {
            Assert.True(RainHubLinkState.ShouldUnlink(error));
        }

        [Theory]
        [InlineData(RainHubError.None)]
        [InlineData(RainHubError.Unreachable)]
        [InlineData(RainHubError.DeviceNotAccepted)]
        [InlineData(RainHubError.RateLimited)]
        [InlineData(RainHubError.ServiceUnavailable)]
        [InlineData(RainHubError.RequestRejected)]
        [InlineData(RainHubError.BadResponse)]
        public void TransientOrUnrelatedFailuresKeepTheCredential(RainHubError error)
        {
            // This is the important half. Unlinking on a rate limit or an outage
            // would destroy a working link because RainHub was briefly unhealthy.
            Assert.False(RainHubLinkState.ShouldUnlink(error));
        }

        [Fact]
        public void BeingOfflineDoesNotUnlinkTheDevice()
        {
            Assert.False(RainHubLinkState.ShouldUnlink(RainHubError.Unreachable));
        }

        [Fact]
        public void BeingRateLimitedDoesNotUnlinkTheDevice()
        {
            Assert.False(RainHubLinkState.ShouldUnlink(RainHubError.RateLimited));
        }
    }
}