using System.Windows;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for VersionControlPage.xaml
    /// </summary>
    public partial class VersionControlPage
    {
        private readonly VersionControlViewModel _viewModel = new();

        public VersionControlPage()
        {
            InitializeComponent();

            DataContext = _viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Selection and install state can change while this page is not
                // on screen (a background install, a launch from the tray), so
                // those are re-read every time. The catalogue itself is only
                // fetched on an explicit refresh, so opening the page never
                // silently triggers network traffic.
                _viewModel.RefreshSelectionState();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VersionControlPage::Page_Loaded", ex);
            }

            await Task.CompletedTask;
        }
    }
}