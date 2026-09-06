using System.Windows;

using Bloxstrap.Plugins;
using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for MultiInstancePage.xaml
    /// </summary>
    public partial class MultiInstancePage
    {
        private bool _initialLoad = false;

        private MultiInstanceViewModel _viewModel = null!;

        public MultiInstancePage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
            }

            if (App.PluginManager is null ||
                !App.PluginManager.Plugins.TryGetValue("rainstrap.multiinstance", out var plugin) ||
                plugin is not MultiInstancePlugin miPlugin)
            {
                DataContext = null;
                return;
            }

            if (DataContext is not MultiInstanceViewModel)
            {
                _viewModel = new MultiInstanceViewModel(miPlugin);
                DataContext = _viewModel;
            }
            else
            {
                _viewModel.Refresh();
            }
        }
    }
}
