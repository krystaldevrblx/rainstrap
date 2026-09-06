using Bloxstrap.Plugins;
using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ClipsPage
    {
        private bool _initialLoad = false;
        private ClipsViewModel _viewModel = null!;

        public ClipsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            if (App.PluginManager is null ||
                !App.PluginManager.Plugins.TryGetValue("rainstrap.clips", out var plugin) ||
                plugin is not ClipsPlugin clipsPlugin ||
                clipsPlugin.Manager is null)
            {
                return;
            }

            _viewModel = new ClipsViewModel(clipsPlugin.Manager, clipsPlugin);
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            _viewModel?.Refresh();
        }

        private void Page_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
        }
    }
}
