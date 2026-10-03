using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

using Bloxstrap.UI.ViewModels.Settings;
using Bloxstrap.UI.Elements.Settings.Pages;
using Bloxstrap.UI.Elements.Controls;
using Bloxstrap.Integrations.RainHub;

namespace Bloxstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        // we should cache this
        private List<SearchBarItem>? _searchIndex;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            gbs.Opacity = viewModel.GBSEnabled ? 1 : 0.5;
            gbs.IsEnabled = viewModel.GBSEnabled; // binding doesnt work as expected so we are setting it in here instead

            ApplyRainHubTabVisibility();

            LoadState();

            string? lastPageName = App.State.Prop.LastPage;
            Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

            App.RemoteData.Subscribe((object? sender, EventArgs e) => {
                RemoteDataBase Data = App.RemoteData.Prop;

                AlertBar.Visibility = Data.AlertEnabled ? Visibility.Visible : Visibility.Collapsed;
                AlertBar.Message = Data.AlertContent;
                AlertBar.Severity = Data.AlertSeverity;

                if (Data.KillFlags)
                    fastflags.PageType = typeof(FastFlagsDisabled);
            });

            // The RainHub tab appears and disappears with the link, including when the
            // link is dropped from inside the page itself. Re-evaluated on navigation
            // so a page that unlinks itself is reflected as soon as the user leaves.
            RootNavigation.Navigated += OnNavigation!;

            void OnNavigation(object? sender, RoutedNavigationEventArgs e)
            {
                INavigationItem? currentPage = RootNavigation.Current;

                App.State.Prop.LastPage = currentPage?.PageType.FullName!;

                ApplyRainHubTabVisibility();
            }

            if (lastPage != null)
                SafeNavigate(lastPage);

            // run scraper
            this.Loaded += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    BuildSearchIndexAutomatically();
                }, System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        protected override void OnApplyTheme()
        {
            base.OnApplyTheme();

            // the base constructor applies the theme before InitializeComponent runs
            if (!IsInitialized)
                return;

            RainLayer.Refresh();
        }

        /// <summary>
        /// Repaints the rain layer without re-applying the whole theme. Turning the rain
        /// off only affects the falling streaks - the gradient behind it is untouched.
        /// </summary>
        public void ApplyRainBackground() => OnApplyTheme();

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        /// <summary>
        /// Shows the RainHub tab only while an account is actually linked.
        ///
        /// Shown conditionally rather than always: with no link the page can do nothing
        /// except offer to link, which belongs in settings, and an inviting tab that
        /// leads to a dead end is worse than no tab. Set in code because the same
        /// reason that applies to the Global Settings item - the binding is not
        /// evaluated reliably by NavigationFluent.
        /// </summary>
        private void ApplyRainHubTabVisibility()
        {
            bool linked = RainHubAccount.IsLinked;

            rainhub.Visibility = linked ? Visibility.Visible : Visibility.Collapsed;

            // The remembered last page is still valid while linked, but must not be
            // restored onto a tab that is hidden.
            if (!linked && RootNavigation.Current?.PageType == typeof(RainHubPage))
                SafeNavigate(typeof(BehaviourPage));
        }

        private async void SafeNavigate(Type page)
        {
            await Task.Delay(500); // same as below

            if (page == typeof(GlobalSettingsPage) && !App.GlobalSettings.Loaded)
                return; // prevent from navigating onto disabled page

            Navigate(page);
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        private void BuildSearchIndexAutomatically()
        {
            _searchIndex = new List<SearchBarItem>();

            var navItems = RootNavigation.Items.OfType<NavigationItem>();

            foreach (var item in navItems)
            {
                if (item.PageType == null)
                    continue;

                if (Activator.CreateInstance(item.PageType) is Page pageInstance)
                {
                    var optionControls = FindLogicalChildren<OptionControl>(pageInstance);

                    foreach (var optionControl in optionControls)
                    {
                        if (optionControl.Header is string headerText && !string.IsNullOrWhiteSpace(headerText))
                        {
                            _searchIndex.Add(new SearchBarItem
                            {
                                DisplayName = headerText,
                                PageType = item.PageType
                            });
                        }
                    }
                }
            }
        }

        private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            foreach (object rawChild in LogicalTreeHelper.GetChildren(depObj))
            {
                if (rawChild is DependencyObject child)
                {
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindLogicalChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        // should move to viewmodels but uhhh im kinda lazy
        private void AutoSuggestBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is AutoSuggestBox autoSuggestBox)
            {
                var currentText = autoSuggestBox.Text;

                if (string.IsNullOrWhiteSpace(currentText))
                {
                    autoSuggestBox.ItemsSource = null;
                    return;
                }

                var selectedSetting = _searchIndex?.FirstOrDefault(x => x.DisplayName.Equals(currentText, StringComparison.OrdinalIgnoreCase));

                if (selectedSetting is not null)
                {
                    Navigate(selectedSetting.PageType);
                    return;
                }

                var query = currentText.ToLower();
                autoSuggestBox.ItemsSource = _searchIndex?
                    .Where(x => x.DisplayName.ToLower().Contains(query))
                    .Select(x => x.DisplayName)
                    .ToList();
            }
        }
    }
}