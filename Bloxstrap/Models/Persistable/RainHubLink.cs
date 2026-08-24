namespace Bloxstrap.Models.Persistable
{
    /// <summary>
    /// Persisted RainHub ↔ Rainstrap link state (stored as RainHubLink.json).
    ///
    /// SECURITY: this file only ever contains the RainHub device token and
    /// display metadata. Roblox authentication cookies are NEVER stored here or
    /// sent to RainHub.
    /// </summary>
    public class RainHubLink
    {
        /// <summary>
        /// Whether the link is active. The sync loop only runs when this is set,
        /// so Rainstrap stays fully functional offline / unlinked.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Optional override for the RainHub API base URL (self-hosters).
        /// When empty (the default), the built-in per-build default is used:
        /// DEBUG builds target the local development API, release builds target
        /// the production RainHub API. A stale value pointing at the retired
        /// legacy domain is ignored automatically.
        /// </summary>
        public string ApiBase { get; set; } = "";

        /// <summary>
        /// Public device identifier issued by RainHub during pairing.
        /// </summary>
        public string DeviceId { get; set; } = "";

        /// <summary>
        /// Bearer token used for the /api/device/* endpoints.
        /// </summary>
        public string DeviceToken { get; set; } = "";

        /// <summary>
        /// Human-readable label of the linked RainHub account (display only).
        /// </summary>
        public string AccountLabel { get; set; } = "";

        /// <summary>
        /// When the device was paired.
        /// </summary>
        public DateTime LinkedAt { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Name reported to RainHub (defaults to machine name at pairing time).
        /// </summary>
        public string DeviceName { get; set; } = "";
    }
}
