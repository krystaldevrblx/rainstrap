using System.Windows;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for RainHubPage.xaml
    /// </summary>
    public partial class RainHubPage
    {
        private bool _initialLoad = false;

        private RainHubViewModel _viewModel = null!;

        public RainHubPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            // Recreate the VM so link status / backup presence reflect reality.
            _viewModel = new RainHubViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }
    }
}
