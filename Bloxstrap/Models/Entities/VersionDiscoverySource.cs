namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// Where a catalogue entry came from.
    ///
    /// Recorded per entry rather than globally, because it decides how much the UI
    /// is allowed to claim. A channel version is authoritative; a locally observed
    /// one is a lead that still has to be verified before the user is told it exists
    /// in any meaningful sense.
    /// </summary>
    public enum VersionDiscoverySource
    {
        /// <summary>Reported by the channel's current deployment info.</summary>
        ChannelDeployment = 0,

        /// <summary>Known only from this machine's history or an on-disk folder.</summary>
        ObservedLocally = 1,
    }
}