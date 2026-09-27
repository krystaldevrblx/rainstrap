namespace Bloxstrap.Enums
{
    /// <summary>
    /// How Quick Join should choose between the servers RainHub returns.
    ///
    /// This is intentionally a preference over RainHub's own result set, not a second
    /// server-selection algorithm. RainHub performs the fetching, filtering and sorting;
    /// Rainstrap only picks which of the returned servers to hand to the launcher.
    ///
    /// Only strategies the available data can actually support are listed. In
    /// particular there is no region option, because RainHub does not currently expose
    /// per-server region data, and no "newest server" option, because it does not expose
    /// server age. Adding either requires the backend to start providing that data first
    /// - they are not faked here.
    /// </summary>
    public enum QuickJoinPreference
    {
        /// <summary>Best ping, preferring a busier server among equals. The default.</summary>
        [EnumName(FromTranslation = "RainHub_QuickJoin_Balanced")]
        Balanced,

        /// <summary>Emptiest server first - best for farming or trading.</summary>
        [EnumName(FromTranslation = "RainHub_QuickJoin_Lowest")]
        LowestPopulation,

        /// <summary>Busiest server first.</summary>
        [EnumName(FromTranslation = "RainHub_QuickJoin_Highest")]
        HighestPopulation,

        /// <summary>Lowest reported ping. Falls back to Balanced when no server reports ping.</summary>
        [EnumName(FromTranslation = "RainHub_QuickJoin_Latency")]
        LowestLatency,

        /// <summary>A random joinable server.</summary>
        [EnumName(FromTranslation = "RainHub_QuickJoin_Random")]
        Random
    }
}
