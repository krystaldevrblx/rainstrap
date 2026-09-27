using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for RainHubPage.xaml
    /// </summary>
    public partial class RainHubPage
    {
        private readonly RainHubViewModel _viewModel = new();

        public RainHubPage()
        {
            InitializeComponent();

            DataContext = _viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.OnPageLoadedAsync();
            }
            catch (TaskCanceledException)
            {
                // Navigated away mid-request; nothing to report.
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("RainHubPage::Page_Loaded", ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop in-flight RainHub work so a hidden page does not keep polling.
            _viewModel.OnPageUnloaded();
        }

        #region Keyboard shortcuts

        /// <summary>
        /// Enter submits rather than moving focus. Both search and pairing are single-field
        /// forms, so Enter is the only shortcut a user needs and the default behaviour
        /// (focus traversal) just walks away from the field they were typing in.
        /// </summary>
        private void GameSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (_viewModel.CanSearch)
                _viewModel.SearchGamesCommand.Execute(null);
        }

        private void PairCodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (_viewModel.CanLink)
                _viewModel.LinkCommand.Execute(null);
        }

        #endregion

        #region Row selection

        /// <summary>
        /// A click on the row body selects it. Clicks that landed on the row's own button are
        /// left to that button, otherwise a single click would run the selection twice and
        /// fire two identical server requests at RainHub.
        /// </summary>
        private void SelectableRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (CameFromButton(e.OriginalSource))
                return;

            SelectRow(e.OriginalSource as FrameworkElement);
        }

        private void SelectableRow_Button_Click(object sender, RoutedEventArgs e)
            => SelectRow(sender as FrameworkElement);

        private void ServerRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (CameFromButton(e.OriginalSource))
                return;

            InspectServer(e.OriginalSource as FrameworkElement);
        }

        private void SelectRow(FrameworkElement? element)
        {
            if (element?.DataContext is RainHubSearchGameItem item)
                _viewModel.SelectSearchResultCommand.Execute(item);
        }

        private void InspectServer(FrameworkElement? element)
        {
            if (element?.DataContext is RainHubServerItem item)
                _viewModel.InspectServerCommand.Execute(item);
        }

        private static bool CameFromButton(object? originalSource)
        {
            if (originalSource is not DependencyObject current)
                return false;

            while (current is not null)
            {
                if (current is ButtonBase)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        #endregion
    }
}
