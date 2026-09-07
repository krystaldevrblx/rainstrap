using System.Windows;
using System.Windows.Controls;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class PluginsPage
    {
        private bool _initialLoad = false;

        private PluginsViewModel ViewModel => (PluginsViewModel)DataContext;

        public PluginsPage()
        {
            DataContext = new PluginsViewModel();
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            await ViewModel.RefreshAsync();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pluginId)
            {
                var card = ViewModel.AvailablePlugins.FirstOrDefault(p => p.Id == pluginId);
                if (card is not null)
                {
                    await ViewModel.InstallPluginAsync(card);
                }
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pluginId)
            {
                await ViewModel.UpdatePluginAsync(pluginId);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pluginId)
            {
                ViewModel.UninstallPlugin(pluginId);
            }
        }
    }
}
