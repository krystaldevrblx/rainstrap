using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Enums;
using Bloxstrap.Integrations.RainHub;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// The RainHub tab: a native client for the RainHub API.
    ///
    /// Layering:
    ///   RainHub            - authoritative Roblox data, server discovery, selection
    ///   RainHubClient      - HTTP only
    ///   this view model    - state, formatting, cancellation
    ///   RainHubPage        - presentation
    ///
    /// Polling policy: nothing polls on a timer. Data is fetched when the page loads,
    /// when the user asks for it, and when a filter changes. In-flight requests are
    /// cancelled on unload, so navigating away does not leave work running against the
    /// API.
    /// </summary>
    public class RainHubViewModel : NotifyPropertyChangedViewModel
    {
        private CancellationTokenSource _cts = new();

        public RainHubViewModel()
        {
            LinkCommand = new AsyncRelayCommand(LinkAsync);
            UnlinkCommand = new RelayCommand(Unlink);
            OpenRainHubWebsiteCommand = new RelayCommand(() => Utilities.ShellExecute(RainHubClient.WebsiteUrl));
            OpenRainHubDevicesCommand = new RelayCommand(() => Utilities.ShellExecute(RainHubClient.DevicesUrl));

            RefreshAllCommand = new AsyncRelayCommand(LoadAllAsync);

            SearchGamesCommand = new AsyncRelayCommand(() => SearchGamesAsync(_cts.Token));
            RefreshServersCommand = new AsyncRelayCommand(() => RefreshServersAsync(_cts.Token));
            SelectSearchResultCommand = new RelayCommand<RainHubSearchGameItem>(SelectGame);
            JoinServerCommand = new RelayCommand<RainHubServerItem>(JoinServer);
            InspectServerCommand = new RelayCommand<RainHubServerItem>(InspectServer);
            JoinQuickCommand = new AsyncRelayCommand(() => QuickJoinAsync(_cts.Token));

            RefreshDiscoveryCommand = new AsyncRelayCommand(() => LoadDiscoveryAsync(_cts.Token));
            PlayGameCommand = new RelayCommand<RainHubGameItem>(PlayGame);
            FindServersCommand = new RelayCommand<RainHubGameItem>(FindServersForGame);
            OpenGameOnRainHubCommand = new RelayCommand<RainHubGameItem>(OpenGameOnRoblox);
        }

        #region Commands

        public ICommand LinkCommand { get; }
        public ICommand UnlinkCommand { get; }
        public ICommand OpenRainHubWebsiteCommand { get; }
        public ICommand OpenRainHubDevicesCommand { get; }
        public ICommand RefreshAllCommand { get; }
        public ICommand SearchGamesCommand { get; }
        public ICommand RefreshServersCommand { get; }
        public ICommand SelectSearchResultCommand { get; }
        public ICommand JoinServerCommand { get; }
        public ICommand InspectServerCommand { get; }
        public ICommand JoinQuickCommand { get; }
        public ICommand RefreshDiscoveryCommand { get; }
        public ICommand PlayGameCommand { get; }
        public ICommand FindServersCommand { get; }
        public ICommand OpenGameOnRainHubCommand { get; }

        #endregion

        #region Link state

        public bool IsLinked => RainHubAccount.IsLinked;
        public bool IsNotLinked => !IsLinked;

        public string DeviceIdText => RainHubAccount.DeviceId is { } id
            ? string.Format(Strings.RainHub_DeviceIdFormat, id)
            : "";

        public string LastHeartbeatText => App.RainHubAccount.Prop.LastHeartbeatAt is { } hb
            ? string.Format(Strings.RainHub_LastSeenFormat, hb.ToLocalTime().ToString("g"))
            : Strings.RainHub_NoHeartbeatYet;

        public string DeviceName => Environment.MachineName;

        private string _linkCode = "";

        /// <summary>
        /// Normalises as the user types, so the code never has to be retyped because of
        /// case, whitespace or a stray character. RainHub's codes are uppercase
        /// alphanumeric, so anything else is dropped rather than rejected on submit.
        /// </summary>
        public string LinkCode
        {
            get => _linkCode;
            set
            {
                string cleaned = NormaliseLinkCode(value);

                if (cleaned == _linkCode)
                    return;

                _linkCode = cleaned;
                OnPropertyChanged(nameof(LinkCode));
                OnPropertyChanged(nameof(CanLink));
            }
        }

        private static string NormaliseLinkCode(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var builder = new System.Text.StringBuilder(PairCodeLength);

            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c))
                    continue;

                builder.Append(char.ToUpperInvariant(c));

                if (builder.Length == PairCodeLength)
                    break;
            }

            return builder.ToString();
        }

        private const int PairCodeLength = 6;

        private bool _isLinking;
        public bool IsLinking
        {
            get => _isLinking;
            private set
            {
                _isLinking = value;
                OnPropertyChanged(nameof(IsLinking));
                OnPropertyChanged(nameof(CanLink));
            }
        }

        public bool CanLink => !IsLinking && LinkCode.Length == PairCodeLength;

        private async Task LinkAsync()
        {
            if (!CanLink)
                return;

            IsLinking = true;
            ClearError();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            RainHubResult<RainHubAccount> result;

            try
            {
                result = await App.RainHubAccount
                    .LinkAsync(LinkCode, DeviceName, cts.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // The user navigated away mid-link, or RainHub never answered. Either way
                // the device is not linked and the form is still filled in for a retry.
                IsLinking = false;
                SetError(Strings.RainHub_LinkFailed);
                return;
            }
            catch (Exception ex)
            {
                IsLinking = false;
                App.Logger.WriteException("RainHubViewModel::LinkAsync", ex);
                SetError(Strings.RainHub_LinkFailed);
                return;
            }

            IsLinking = false;

            if (result.Success)
            {
                LinkCode = "";
                RaiseLinkStateChanged();
                await LoadAllAsync();
            }
            else
            {
                SetError(result.Message ?? Strings.RainHub_LinkFailed);
            }
        }

        private void Unlink()
        {
            // Unlinking throws away the only copy of the device credential this machine
            // holds, and the replacement is a generated code on another device, so it is
            // worth one confirmation.
            if (Frontend.ShowMessageBox(
                    Strings.RainHub_Unlink_Confirm,
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            App.RainHubAccount.Unlink();
            ClearError();
            ResetData();
            RaiseLinkStateChanged();
        }

        private void RaiseLinkStateChanged()
        {
            OnPropertyChanged(nameof(IsLinked));
            OnPropertyChanged(nameof(IsNotLinked));
            OnPropertyChanged(nameof(DeviceIdText));
            OnPropertyChanged(nameof(LastHeartbeatText));
        }

        #endregion

        #region Error surface

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value)
                    return;

                _errorMessage = value;

                OnPropertyChanged(nameof(ErrorMessage));

                // HasError is what the page binds, so it has to be announced alongside the
                // message or the banner silently keeps its previous state.
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        /// <summary>
        /// Two-way proxy for the error banner's open state.
        ///
        /// The banner's own close button writes <c>IsOpen = false</c> on itself. Without a
        /// setter here that would either be a one-way binding the user could permanently
        /// break, or a model that still believed an error was pending. Clearing the message
        /// is the only honest answer to "dismiss".
        /// </summary>
        public bool IsErrorOpen
        {
            get => HasError;
            set
            {
                if (!value)
                    ClearError();
            }
        }

        /// <summary>
        /// Set when RainHub rejected this device's credential, so the page can offer a
        /// relink instead of leaving a dead error message the user cannot act on.
        /// </summary>
        public bool NeedsRelink { get; private set; }

        public bool HasNoRelinkNeeded => !NeedsRelink;

        private void SetError(string message) => ErrorMessage = message;

        private void ClearError()
        {
            ErrorMessage = "";
            NeedsRelink = false;
            OnPropertyChanged(nameof(NeedsRelink));
            OnPropertyChanged(nameof(HasNoRelinkNeeded));
        }

        /// <summary>
        /// Routes a failed RainHub call to the right message. Only a rejected credential
        /// asks the user to link again - an endpoint that simply does not accept device
        /// links must not send them through a pointless relink.
        /// </summary>
        private void SetError(RainHubError error, string? message, string fallback)
        {
            ErrorMessage = message ?? fallback;

            bool relink = error == RainHubError.NotLinked;

            if (relink != NeedsRelink)
            {
                NeedsRelink = relink;
                OnPropertyChanged(nameof(NeedsRelink));
                OnPropertyChanged(nameof(HasNoRelinkNeeded));
            }
        }

        #endregion

        #region Server finder

        public ObservableCollection<RainHubSearchGameItem> SearchResults { get; } = new();
        public ObservableCollection<RainHubServerItem> Servers { get; } = new();

        private string _gameSearchText = "";
        public string GameSearchText
        {
            get => _gameSearchText;
            set
            {
                _gameSearchText = value ?? "";
                OnPropertyChanged(nameof(GameSearchText));
                OnPropertyChanged(nameof(CanSearch));
            }
        }

        public bool CanSearch => GameSearchText.Trim().Length >= 2 && !IsSearchingGames;

        /// <summary>Monotonic id of the most recent search request.</summary>
        private int _searchRequestId;

        private RainHubSearchGameItem? _selectedGame;
        public RainHubSearchGameItem? SelectedGame
        {
            get => _selectedGame;
            private set
            {
                _selectedGame = value;
                OnPropertyChanged(nameof(SelectedGame));
                OnPropertyChanged(nameof(HasSelectedGame));
                OnPropertyChanged(nameof(CanRefreshServers));
                OnPropertyChanged(nameof(ServersEmptyText));
                OnPropertyChanged(nameof(ServersCountText));
            }
        }

        public bool HasSelectedGame => SelectedGame is not null;
        public bool HasSearchResults => SearchResults.Count > 0;
        public bool CanRefreshServers => SelectedGame is not null && !IsLoadingServers;

        public IReadOnlyList<QuickJoinPreference> QuickJoinPreferences { get; } =
            Enum.GetValues(typeof(QuickJoinPreference)).Cast<QuickJoinPreference>().ToList();

        public QuickJoinPreference SelectedQuickJoinPreference
        {
            get => App.Settings.Prop.RainHubQuickJoinPreference;
            set
            {
                if (App.Settings.Prop.RainHubQuickJoinPreference == value)
                    return;

                App.Settings.Prop.RainHubQuickJoinPreference = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(SelectedQuickJoinPreference));
            }
        }

        public IReadOnlyList<string> ServerFilterLabels { get; } = new[]
        {
            Strings.RainHub_Filter_All,
            Strings.RainHub_Filter_Empty,
            Strings.RainHub_Filter_Low,
            Strings.RainHub_Filter_Full,
            Strings.RainHub_Filter_Richest
        };

        public IReadOnlyList<string> ServerSortLabels { get; } = new[]
        {
            Strings.RainHub_Sort_MostPlayers,
            Strings.RainHub_Sort_FewestPlayers,
            Strings.RainHub_Sort_BestPing
        };

        /// <summary>
        /// The filter/sort the combo box index currently means.
        ///
        /// The index is cast to the enum through these rather than inline, so an index that
        /// somehow falls outside the enum cannot be sent to RainHub as a nonsense value -
        /// the label list and the enum are declared side by side and this is the one place
        /// that relationship is expressed.
        /// </summary>
        private static readonly RainHubServerFilter[] Filters =
            (RainHubServerFilter[])Enum.GetValues(typeof(RainHubServerFilter));

        private static readonly RainHubServerSort[] Sorts =
            (RainHubServerSort[])Enum.GetValues(typeof(RainHubServerSort));

        private RainHubServerFilter SelectedFilter =>
            SelectedFilterIndex >= 0 && SelectedFilterIndex < Filters.Length
                ? Filters[SelectedFilterIndex]
                : RainHubServerFilter.All;

        private RainHubServerSort SelectedSort =>
            SelectedSortIndex >= 0 && SelectedSortIndex < Sorts.Length
                ? Sorts[SelectedSortIndex]
                : RainHubServerSort.Fullness;

        private int _selectedFilterIndex;
        public int SelectedFilterIndex
        {
            get => _selectedFilterIndex;
            set
            {
                if (_selectedFilterIndex == value)
                    return;

                _selectedFilterIndex = value;
                OnPropertyChanged(nameof(SelectedFilterIndex));

                if (SelectedGame is not null)
                    _ = RefreshServersAsync(_cts.Token);
            }
        }

        private int _selectedSortIndex;
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                if (_selectedSortIndex == value)
                    return;

                _selectedSortIndex = value;
                OnPropertyChanged(nameof(SelectedSortIndex));

                if (SelectedGame is not null)
                    _ = RefreshServersAsync(_cts.Token);
            }
        }

        private bool _isSearchingGames;
        public bool IsSearchingGames
        {
            get => _isSearchingGames;
            private set
            {
                _isSearchingGames = value;
                OnPropertyChanged(nameof(IsSearchingGames));
                OnPropertyChanged(nameof(CanSearch));
            }
        }

        private bool _isLoadingServers;
        public bool IsLoadingServers
        {
            get => _isLoadingServers;
            private set
            {
                _isLoadingServers = value;
                OnPropertyChanged(nameof(IsLoadingServers));
                OnPropertyChanged(nameof(CanRefreshServers));
                OnPropertyChanged(nameof(ServersEmptyText));
                OnPropertyChanged(nameof(ServersCountText));
            }
        }

        public string ServersEmptyText
        {
            get
            {
                if (IsLoadingServers)
                    return "";

                if (Servers.Count > 0)
                    return "";

                return HasSelectedGame
                    ? Strings.RainHub_NoServersMatch
                    : Strings.RainHub_Servers_SelectPrompt;
            }
        }

        /// <summary>How many servers RainHub actually returned for the current filter.</summary>
        public string ServersCountText => IsLoadingServers || Servers.Count == 0
            ? ""
            : string.Format(Strings.RainHub_Servers_CountFormat, Servers.Count);

        /// <summary>
        /// Resolves what the user typed. A bare place id or a roblox.com link goes straight
        /// to RainHub's resolver, because those are not game names and the search endpoint
        /// has nothing to match them against - which is what previously made pasting a link
        /// look like RainHub was broken. Anything else is a name search.
        /// </summary>
        private async Task SearchGamesAsync(CancellationToken token)
        {
            string query = GameSearchText.Trim();

            if (query.Length < 2)
                return;

            IsSearchingGames = true;

            // Same guard as the server list: a second search started before the first
            // finished must not be overwritten by the first one's slower response.
            int requestId = ++_searchRequestId;

            RainHubResult<List<RainHubSearchGame>> result;

            try
            {
                if (RainHubClient.TryExtractPlaceId(query, out string placeId))
                {
                    var resolved = await RainHubClient
                        .ResolvePlaceAsync(placeId, token)
                        .ConfigureAwait(true);

                    result = resolved.Success && resolved.Value is not null
                        ? RainHubResult<List<RainHubSearchGame>>.Ok(new List<RainHubSearchGame>
                        {
                            new()
                            {
                                PlaceId = resolved.Value.PlaceId,
                                Name = resolved.Value.Name,
                                ThumbnailUrl = resolved.Value.ThumbnailUrl,
                                PlayerCount = resolved.Value.ActivePlayerCount,
                                MaxPlayers = resolved.Value.MaxPlayers
                            }
                        })
                        : RainHubResult<List<RainHubSearchGame>>.Fail(resolved.Error, resolved.Message);
                }
                else
                {
                    result = await RainHubClient.SearchGamesAsync(query, token).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                if (requestId == _searchRequestId)
                    IsSearchingGames = false;

                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubViewModel::SearchGamesAsync", ex);

                if (requestId == _searchRequestId)
                {
                    IsSearchingGames = false;
                    SetError(Strings.RainHub_SearchFailed);
                }

                return;
            }

            if (requestId != _searchRequestId || token.IsCancellationRequested)
                return;

            IsSearchingGames = false;

            if (token.IsCancellationRequested)
                return;

            SearchResults.Clear();

            if (!result.Success || result.Value is null)
            {
                OnPropertyChanged(nameof(HasSearchResults));
                SetError(result.Error, result.Message, Strings.RainHub_SearchFailed);
                return;
            }

            if (result.Value.Count == 0)
            {
                OnPropertyChanged(nameof(HasSearchResults));
                SetError(Strings.RainHub_SearchNoResults);
                return;
            }

            foreach (var game in result.Value)
                SearchResults.Add(new RainHubSearchGameItem(game));

            OnPropertyChanged(nameof(HasSearchResults));

            // A place id or link names exactly one game, so showing the user a one row
            // list they still have to click is busywork. Resolve straight through to servers.
            if (result.Value.Count == 1 && RainHubClient.TryExtractPlaceId(query, out _))
                SelectGame(SearchResults[0]);
        }

        private void SelectGame(RainHubSearchGameItem? item)
        {
            if (item is null)
                return;

            UseGame(item.PlaceId, item.Name, item.UniverseId);
            _ = RefreshServersAsync(_cts.Token);
        }

        private void UseGame(string placeId, string? name, string? universeId = null)
        {
            SelectedGame = new RainHubSearchGameItem(new RainHubSearchGame
            {
                PlaceId = placeId,
                UniverseId = universeId ?? "",
                Name = string.IsNullOrWhiteSpace(name) ? placeId : name!
            });

            GameSearchText = SelectedGame.Name;

            // The previous server list belonged to a different game.
            ClearSelectedServer();
        }

        private async Task RefreshServersAsync(CancellationToken token)
        {
            if (SelectedGame is null)
                return;

            // Changing the filter or the sort starts another load without waiting for the
            // last one. Tagging each request means a slow earlier response cannot land
            // after a faster later one and repopulate the list with the wrong server set.
            int requestId = ++_serverRequestId;

            IsLoadingServers = true;

            RainHubResult<RainHubServerList> result;

            try
            {
                // RainHub performs the filtering and sorting; these are passed straight
                // through as its documented query parameters.
                result = await RainHubClient.GetServersAsync(
                    SelectedGame.PlaceId,
                    SelectedFilter,
                    SelectedSort,
                    token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                if (requestId == _serverRequestId)
                    IsLoadingServers = false;

                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubViewModel::RefreshServersAsync", ex);

                if (requestId == _serverRequestId)
                {
                    IsLoadingServers = false;
                    SetError(Strings.RainHub_ServersFailed);
                }

                return;
            }

            if (requestId != _serverRequestId || token.IsCancellationRequested)
                return;

            IsLoadingServers = false;

            if (!result.Success || result.Value is null)
            {
                Servers.Clear();
                ClearSelectedServer();
                RaiseServersChanged();
                SetError(result.Error, result.Message, Strings.RainHub_ServersFailed);
                return;
            }

            // RainHub returns an empty list plus a reason when it could not produce one
            // (browsing disabled, rate limited, game not found). Show that reason rather
            // than a bare "no servers".
            if (result.Value.Servers.Count == 0)
            {
                Servers.Clear();
                ClearSelectedServer();
                RaiseServersChanged();

                if (!string.IsNullOrEmpty(result.Value.Error))
                    SetError(result.Value.Error!);

                return;
            }

            Servers.Clear();

            foreach (var server in result.Value.Servers)
                Servers.Add(new RainHubServerItem(server));

            // The server list was replaced, so whatever was being inspected is gone.
            ClearSelectedServer();
            RaiseServersChanged();
        }

        /// <summary>Monotonic id of the most recent server list request.</summary>
        private int _serverRequestId;

        private void RaiseServersChanged()
        {
            OnPropertyChanged(nameof(HasServers));
            OnPropertyChanged(nameof(ServersEmptyText));
            OnPropertyChanged(nameof(ServersCountText));
        }

        public bool HasServers => Servers.Count > 0;

        private void JoinServer(RainHubServerItem? server)
        {
            if (server is null || SelectedGame is null)
                return;

            ReportJoin(RainHubJoin.Join(SelectedGame.PlaceId, server.JobId));
        }

        /// <summary>Turns a join attempt into a message, or clears the error on success.</summary>
        private void ReportJoin(RainHubJoin.JoinOutcome outcome)
        {
            switch (outcome)
            {
                case RainHubJoin.JoinOutcome.Started:
                    ClearError();

                    // A join is the strongest liveness signal there is, and it is exactly
                    // when RainHub's "last seen" should move.
                    _ = SendHeartbeatAsync();
                    break;

                case RainHubJoin.JoinOutcome.RobloxNotInstalled:
                    SetError(Strings.RainHub_RobloxNotInstalled);
                    break;

                default:
                    // Most often the server closed between the list being fetched and the
                    // join being requested, which the user cannot act on beyond retrying.
                    SetError(Strings.RainHub_JoinFailed);
                    break;
            }
        }

        private void InspectServer(RainHubServerItem? server)
        {
            if (server is null)
                return;

            SetInspectedServer(server);
        }

        private RainHubServerItem? _selectedServer;

        /// <summary>
        /// The server shown in the "what am I joining?" panel. Driven by the user clicking
        /// a row, so the join they commit to is always one they have looked at.
        /// </summary>
        public RainHubServerItem? SelectedServer
        {
            get => _selectedServer;
            private set
            {
                _selectedServer = value;
                OnPropertyChanged(nameof(SelectedServer));
                OnPropertyChanged(nameof(HasSelectedServer));
                OnPropertyChanged(nameof(HasNoSelectedServer));
                OnPropertyChanged(nameof(WhatAmIJoiningEmptyText));
            }
        }

        public bool HasSelectedServer => SelectedServer is not null;

        /// <summary>Inverse of <see cref="HasSelectedServer"/>, for the empty state.</summary>
        public bool HasNoSelectedServer => SelectedServer is null;

        public string WhatAmIJoiningEmptyText =>
            HasSelectedServer ? "" : Strings.RainHub_WhatAmIJoining_SelectPrompt;

        /// <summary>
        /// The only writer of <see cref="SelectedServer"/>, so the row marker and the
        /// details panel can never disagree about which server is being inspected.
        /// </summary>
        private void SetInspectedServer(RainHubServerItem? item)
        {
            if (ReferenceEquals(_selectedServer, item))
                return;

            if (_selectedServer is not null)
                _selectedServer.IsInspected = false;

            SelectedServer = item;

            if (item is not null)
                item.IsInspected = true;
        }

        private void ClearSelectedServer() => SetInspectedServer(null);

        /// <summary>
        /// Quick Join: ask RainHub for a suitable server, apply the user's preference as a
        /// tie-break between what RainHub returned, then launch. RainHub still does the
        /// discovery, filtering and sorting.
        /// </summary>
        private async Task QuickJoinAsync(CancellationToken token)
        {
            if (SelectedGame is null)
            {
                SetError(Strings.RainHub_PickAGameFirst);
                return;
            }

            IsLoadingServers = true;

            RainHubResult<RainHubServerList> result;

            try
            {
                result = await RainHubClient.GetServersAsync(
                    SelectedGame.PlaceId,
                    RainHubServerFilter.Low,
                    RainHubServerSort.Ping,
                    token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                IsLoadingServers = false;
                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubViewModel::QuickJoinAsync", ex);
                IsLoadingServers = false;
                SetError(Strings.RainHub_ServersFailed);
                return;
            }

            IsLoadingServers = false;

            if (token.IsCancellationRequested)
                return;

            if (!result.Success || result.Value is null)
            {
                SetError(result.Error, result.Message, Strings.RainHub_ServersFailed);
                return;
            }

            // Show the candidate list either way, so the join is never a black box.
            Servers.Clear();

            foreach (var server in result.Value.Servers)
                Servers.Add(new RainHubServerItem(server));

            RaiseServersChanged();

            var chosen = RainHubClient.PickQuickJoinServer(
                result.Value.Servers, SelectedQuickJoinPreference);

            if (chosen is null)
            {
                ClearSelectedServer();
                SetError(Strings.RainHub_NoJoinableServers);
                return;
            }

            // Point the details panel at the server that is about to be joined, so the
            // user can see what Quick Join picked.
            SetInspectedServer(Servers.FirstOrDefault(s => s.JobId == chosen.JobId));

            ReportJoin(RainHubJoin.Join(SelectedGame.PlaceId, chosen.JobId));
        }

        #endregion

        #region Discovery

        public ObservableCollection<RainHubGameItem> DiscoveryGames { get; } = new();

        private bool _isLoadingDiscovery;
        public bool IsLoadingDiscovery
        {
            get => _isLoadingDiscovery;
            private set
            {
                _isLoadingDiscovery = value;
                OnPropertyChanged(nameof(IsLoadingDiscovery));
                OnPropertyChanged(nameof(DiscoveryEmptyText));
            }
        }

        public bool HasDiscoveryGames => DiscoveryGames.Count > 0;

        /// <summary>Inverse of <see cref="HasDiscoveryGames"/>, for the empty state.</summary>
        public bool HasNoDiscoveryGames => DiscoveryGames.Count == 0;

        public string DiscoveryEmptyText => IsLoadingDiscovery || HasDiscoveryGames
            ? ""
            : Strings.RainHub_Discovery_Empty;

        private string _discoverySummaryText = "";
        public string DiscoverySummaryText
        {
            get => _discoverySummaryText;
            private set
            {
                _discoverySummaryText = value;
                OnPropertyChanged(nameof(DiscoverySummaryText));
                OnPropertyChanged(nameof(HasDiscoverySummary));
            }
        }

        public bool HasDiscoverySummary => !string.IsNullOrEmpty(DiscoverySummaryText);

        /// <summary>
        /// The genre breakdown RainHub already returned, as a single readable line. Left
        /// blank when RainHub reported no genres rather than padding it with an em dash.
        /// </summary>
        private string _discoveryGenresText = "";
        public string DiscoveryGenresText
        {
            get => _discoveryGenresText;
            private set
            {
                _discoveryGenresText = value;
                OnPropertyChanged(nameof(DiscoveryGenresText));
                OnPropertyChanged(nameof(HasDiscoveryGenres));
            }
        }

        public bool HasDiscoveryGenres => !string.IsNullOrEmpty(DiscoveryGenresText);

        private async Task LoadDiscoveryAsync(CancellationToken token)
        {
            IsLoadingDiscovery = true;

            RainHubResult<RainHubTrendReport> result;

            try
            {
                result = await RainHubClient.GetTrendsAsync(token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                IsLoadingDiscovery = false;
                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubViewModel::LoadDiscoveryAsync", ex);
                IsLoadingDiscovery = false;
                SetError(Strings.RainHub_DiscoveryFailed);
                return;
            }

            IsLoadingDiscovery = false;

            if (token.IsCancellationRequested)
                return;

            if (!result.Success || result.Value is null)
            {
                DiscoveryGames.Clear();
                OnPropertyChanged(nameof(HasDiscoveryGames));
                OnPropertyChanged(nameof(DiscoveryEmptyText));
                SetError(result.Error, result.Message, Strings.RainHub_DiscoveryFailed);
                return;
            }

            DiscoveryGames.Clear();

            foreach (var game in result.Value.Trending)
                DiscoveryGames.Add(new RainHubGameItem(game));

            DiscoverySummaryText = string.Format(
                Strings.RainHub_Discovery_SummaryFormat,
                DiscoveryGames.Count,
                RainHubFormat.Count(result.Value.TotalPlayers));

            DiscoveryGenresText = FormatGenres(result.Value.Genres);

            OnPropertyChanged(nameof(HasDiscoveryGames));
        }

        private static string FormatGenres(IReadOnlyList<RainHubGenreStat> genres)
        {
            if (genres is null || genres.Count == 0)
                return "";

            const int maxGenres = 4;

            string text = string.Join(
                "  •  ",
                genres
                    .Where(g => !string.IsNullOrWhiteSpace(g.Genre))
                    .Take(maxGenres)
                    .Select(g => $"{g.Genre} {RainHubFormat.Percent(g.Popularity)}"));

            return text;
        }

        private void PlayGame(RainHubGameItem? game)
        {
            if (game is null)
                return;

            // No job id: Roblox picks a server itself, which is the point of "play".
            ReportJoin(RainHubJoin.Join(game.PlaceId, null));
        }

        private void FindServersForGame(RainHubGameItem? game)
        {
            if (game is null)
                return;

            UseGame(game.PlaceId, game.Name, game.UniverseId);
            _ = RefreshServersAsync(_cts.Token);
        }

        private void OpenGameOnRoblox(RainHubGameItem? game)
        {
            if (game is null)
                return;

            Utilities.ShellExecute($"https://www.roblox.com/games/{game.PlaceId}");
        }

        #endregion

        #region Heartbeat

        /// <summary>
        /// Tells RainHub this device is alive.
        ///
        /// Fire and forget by design: it is telemetry, so it must never block or fail a
        /// user action. The stored "last seen" timestamp is only updated when RainHub
        /// actually accepted it, so the header never claims liveness that was not confirmed.
        /// </summary>
        private async Task SendHeartbeatAsync()
        {
            if (!IsLinked)
                return;

            CancellationToken token = _cts.Token;

            try
            {
                await App.RainHubAccount
                    .HeartbeatAsync(Utilities.IsRobloxRunning(), token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubViewModel::SendHeartbeatAsync", ex);
                return;
            }

            if (token.IsCancellationRequested)
                return;

            OnPropertyChanged(nameof(LastHeartbeatText));
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Called when the page is shown. Everything on this page needs the link, which
        /// is why the tab is only visible when one exists.
        /// </summary>
        public async Task OnPageLoadedAsync()
        {
            RaiseLinkStateChanged();

            if (!IsLinked)
                return;

            await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            ClearError();

            await SendHeartbeatAsync();

            if (_cts.IsCancellationRequested || !IsLinked)
                return;

            if (SelectedGame is not null)
                await RefreshServersAsync(_cts.Token);

            if (_cts.IsCancellationRequested)
                return;

            await LoadDiscoveryAsync(_cts.Token);
        }

        /// <summary>
        /// Cancels in-flight requests when the user navigates away, so a hidden page does
        /// not keep hitting the RainHub API.
        /// </summary>
        public void OnPageUnloaded()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void ResetData()
        {
            SearchResults.Clear();
            Servers.Clear();
            DiscoveryGames.Clear();
            SelectedGame = null;
            GameSearchText = "";
            DiscoverySummaryText = "";
            DiscoveryGenresText = "";
            ClearSelectedServer();

            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(HasDiscoveryGames));
            OnPropertyChanged(nameof(DiscoveryEmptyText));
            RaiseServersChanged();
        }

        #endregion
    }
}
