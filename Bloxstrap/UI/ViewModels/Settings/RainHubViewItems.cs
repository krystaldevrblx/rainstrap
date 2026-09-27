using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.AppData;
using Bloxstrap.Integrations.RainHub;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// Display wrappers around the RainHub API models.
    ///
    /// These exist for two reasons:
    ///  * Formatting lives in one place instead of in XAML converters.
    ///  * Values RainHub does not provide are shown as "not reported" rather than being
    ///    rendered as 0, blank or an invented number.
    /// </summary>

    public static class RainHubFormat
    {
        public static string Count(int value)
        {
            if (value >= 1_000_000_000) return string.Format(CultureInfo.InvariantCulture, "{0:0.#}B", value / 1_000_000_000d);
            if (value >= 1_000_000) return string.Format(CultureInfo.InvariantCulture, "{0:0.#}M", value / 1_000_000d);
            if (value >= 1_000) return string.Format(CultureInfo.InvariantCulture, "{0:0.#}K", value / 1_000d);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Percent(double ratio) =>
            string.Format(CultureInfo.InvariantCulture, "{0:0}%", ratio * 100);

        public static string NotReported => Strings.RainHub_NotReported;
    }

    public class RainHubSignalItem
    {
        public RainHubSignalItem(RainHubLiveSignal signal)
        {
            Icon = string.IsNullOrEmpty(signal.Icon) ? "📡" : signal.Icon!;
            Text = signal.Text;
            Subtext = signal.Subtext ?? "";
            Type = signal.Type ?? "chart";
            PlaceId = signal.PlaceId;
            GameName = signal.GameName;
            Source = signal.Source;
        }

        public string Icon { get; }
        public string Text { get; }
        public string Subtext { get; }
        public string Type { get; }
        public string? PlaceId { get; }
        public string? GameName { get; }
        public string? Source { get; }

        public bool HasSubtext => !string.IsNullOrEmpty(Subtext);
        public bool HasPlaceId => !string.IsNullOrEmpty(PlaceId);
    }

    public class RainHubSearchGameItem
    {
        public RainHubSearchGameItem(RainHubSearchGame game)
        {
            PlaceId = game.PlaceId;
            UniverseId = game.UniverseId;
            Name = string.IsNullOrWhiteSpace(game.Name) ? "Unknown game" : game.Name;
            Creator = string.IsNullOrWhiteSpace(game.CreatorName) ? "" : game.CreatorName!;
            ThumbnailUrl = game.ThumbnailUrl;
            PlayersText = RainHubFormat.Count(game.PlayerCount);
        }

        public string PlaceId { get; }
        public string UniverseId { get; }
        public string Name { get; }
        public string Creator { get; }
        public string? ThumbnailUrl { get; }
        public string PlayersText { get; }

        public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);
        public bool HasCreator => !string.IsNullOrEmpty(Creator);
        public string PlayersLine => string.Format(Strings.RainHub_PlayersOnlineFormat, PlayersText);
    }

    /// <summary>
    /// One server row. Optional values (ping, fps) are shown as "not reported" when
    /// Roblox did not supply them, and the join button is disabled for full servers.
    /// </summary>
    public class RainHubServerItem : INotifyPropertyChanged
    {
        public RainHubServerItem(RainHubServer server)
        {
            JobId = server.JobId;
            CurrentPlayers = server.CurrentPlayers;
            MaxPlayers = server.MaxPlayers;
            Fullness = server.Fullness;
            Ping = server.Ping;
            PingStatus = server.PingStatus;
            Fps = server.Fps;

            Details = new List<RainHubServerDetail>
            {
                new(Strings.RainHub_WhatAmIJoining_ServerId, JobId),
                new(Strings.RainHub_WhatAmIJoining_Players, PlayersText),
                new(Strings.RainHub_WhatAmIJoining_Fill, FillText),

                // RainHub's spec describes ping as an estimate, so the UI says so rather
                // than presenting it as a measured value.
                new(Strings.RainHub_WhatAmIJoining_PingEstimated, PingText),

                new(Strings.RainHub_WhatAmIJoining_Fps, FpsText),
            };
        }

        public string JobId { get; }
        public int CurrentPlayers { get; }
        public int MaxPlayers { get; }
        public double Fullness { get; }
        public int? Ping { get; }
        public string? PingStatus { get; }
        public double? Fps { get; }

        public string JobIdShort => JobId.Length <= 8 ? JobId : JobId[..8];

        public string PlayersText => string.Format(
            Strings.RainHub_PlayerCountFormat, CurrentPlayers, MaxPlayers);

        public string FillText => RainHubFormat.Percent(Fullness);

        /// <summary>Only meaningful when Roblox actually reported a ping for this server.</summary>
        public string PingText => Ping.HasValue
            ? string.Format(Strings.RainHub_PingFormat, Ping.Value)
            : RainHubFormat.NotReported;

        /// <summary>RainHub's own good/average/poor assessment, when it supplied one.</summary>
        public bool HasPingStatus => !string.IsNullOrEmpty(PingStatus);

        public string FpsText => Fps.HasValue
            ? string.Format(Strings.RainHub_FpsFormat, (int)Math.Round(Fps.Value))
            : RainHubFormat.NotReported;

        public bool HasPing => Ping.HasValue;
        public bool HasFps => Fps.HasValue;
        public bool IsFull => MaxPlayers > 0 && CurrentPlayers >= MaxPlayers;
        public bool CanJoin => !IsFull;

        /// <summary>
        /// Shown on a full server, so the disabled Join button explains itself instead of
        /// looking broken. Roblox will not let a client into a full server, which is why
        /// <see cref="CanJoin"/> is false for these rows.
        /// </summary>
        public string FullText => Strings.RainHub_ServerFull;

        private bool _isInspected;

        /// <summary>
        /// True for the one row the "what am I joining?" panel is currently describing.
        ///
        /// The panel is a separate region of the page, so without a marker on the row itself
        /// there is nothing tying it back to the server the user picked.
        /// </summary>
        public bool IsInspected
        {
            get => _isInspected;
            internal set
            {
                if (_isInspected == value)
                    return;

                _isInspected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInspected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string FillBrushKey => Fullness switch
        {
            >= 0.8 => "RainHub_FillHigh",
            >= 0.4 => "RainHub_FillMedium",
            _ => "RainHub_FillLow"
        };

        /// <summary>
        /// The "what am I joining?" rows.
        ///
        /// Every entry is a value RainHub actually returned for this server. Fields Roblox
        /// did not report show as "not reported" instead of being filled in, and nothing
        /// here is estimated - the one value RainHub itself describes as an estimate
        /// (ping) is labelled as such.
        ///
        /// Server age, place version and region are deliberately absent: RainHub's Server
        /// schema does not return them, so there is nothing real to show.
        /// </summary>
        public IReadOnlyList<RainHubServerDetail> Details { get; }
    }

    /// <summary>One label/value row in the "what am I joining?" panel.</summary>
    public record RainHubServerDetail(string Label, string Value);

    /// <summary>
    /// One Discovery game row. The score shown is RainHub's own discovery score; the
    /// trend is only rendered when RainHub reports that it has enough history.
    /// </summary>
    public class RainHubGameItem
    {
        public RainHubGameItem(RainHubTrendingGame game)
        {
            PlaceId = game.PlaceId;
            UniverseId = game.UniverseId;
            Name = string.IsNullOrWhiteSpace(game.Name) ? "Unknown game" : game.Name;
            ThumbnailUrl = game.ThumbnailUrl;
            Genre = string.IsNullOrWhiteSpace(game.Genre) ? "" : game.Genre!;
            ActivePlayers = game.ActivePlayers;
            Visits = game.Visits;
            DiscoveryScore = game.DiscoveryScore;
            Trend = game.Trend ?? "stable";
            TrendPercent = game.TrendPercent;
            TrendAvailable = game.TrendAvailable ?? false;
        }

        public string PlaceId { get; }
        public string UniverseId { get; }
        public string Name { get; }
        public string? ThumbnailUrl { get; }
        public string Genre { get; }
        public int ActivePlayers { get; }
        public int? Visits { get; }
        public double? DiscoveryScore { get; }
        public string Trend { get; }
        public double? TrendPercent { get; }
        public bool TrendAvailable { get; }

        public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);
        public bool HasGenre => !string.IsNullOrEmpty(Genre);
        public bool HasVisits => Visits.HasValue && Visits.Value > 0;
        public bool HasScore => DiscoveryScore.HasValue;

        public string PlayersText => ActivePlayers > 0
            ? string.Format(Strings.RainHub_PlayersOnlineFormat, RainHubFormat.Count(ActivePlayers))
            : Strings.RainHub_NoLiveData;

        public string VisitsText => HasVisits
            ? string.Format(Strings.RainHub_VisitsFormat, RainHubFormat.Count(Visits!.Value))
            : "";

        public string ScoreText => HasScore
            ? string.Format(Strings.RainHub_ScoreFormat, Math.Round(DiscoveryScore!.Value))
            : RainHubFormat.NotReported;

        /// <summary>
        /// Mirrors the RainHub web UI: "collecting data" until there is a real measured
        /// trend, then a signed percentage.
        /// </summary>
        public string TrendText
        {
            get
            {
                if (!TrendAvailable || TrendPercent is null)
                    return Strings.RainHub_CollectingData;

                double pct = TrendPercent.Value;
                string sign = pct >= 0 ? "+" : "";
                return string.Format(Strings.RainHub_TrendFormat, sign, pct.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// "Play" hands Roblox a place id and no job id, so Roblox starts or waits for a
        /// server itself. That works whether or not RainHub is currently reporting players
        /// for the game, so this is deliberately not gated on <see cref="ActivePlayers"/> -
        /// doing so disabled the button on exactly the games that were worth playing.
        /// </summary>
        public bool CanJoin => !string.IsNullOrEmpty(PlaceId);
    }

    /// <summary>
    /// Launches a specific public server through Roblox's own documented deep link, the
    /// same mechanism Rainstrap already uses for "rejoin server". No process memory
    /// access, injection or flag manipulation is involved.
    /// </summary>
    internal static class RainHubJoin
    {
        private const string LOG_IDENT = "RainHubJoin";

        /// <summary>Why a join could not be started, so the UI can say something useful.</summary>
        public enum JoinOutcome
        {
            Started,

            /// <summary>No Roblox player is installed on this machine.</summary>
            RobloxNotInstalled,

            /// <summary>Roblox is installed but the deep link could not be handed to it.</summary>
            LaunchFailed
        }

        public static JoinOutcome Join(string placeId, string? jobId)
        {
            string playerPath = new RobloxPlayerData().ExecutablePath;

            // RobloxPlayerData only composes a path; it does not guarantee Roblox is
            // actually installed, and Process.Start would otherwise fail with a bare
            // Win32Exception that tells the user nothing.
            if (string.IsNullOrWhiteSpace(playerPath) || !File.Exists(playerPath))
            {
                App.Logger.WriteLine(LOG_IDENT, "Roblox is not installed; cannot start a join.");
                return JoinOutcome.RobloxNotInstalled;
            }

            try
            {
                string uri = $"roblox://experiences/start?placeId={Uri.EscapeDataString(placeId)}";

                if (!string.IsNullOrEmpty(jobId))
                    uri += $"&gameInstanceId={Uri.EscapeDataString(jobId)}";

                Process.Start(playerPath, uri);

                // Job ids are logged, never anything credential-shaped: this is a public
                // server identifier, and it is what makes a failed join diagnosable.
                App.Logger.WriteLine(LOG_IDENT, $"Requested join for place {placeId}");
                return JoinOutcome.Started;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return JoinOutcome.LaunchFailed;
            }
        }
    }
}
