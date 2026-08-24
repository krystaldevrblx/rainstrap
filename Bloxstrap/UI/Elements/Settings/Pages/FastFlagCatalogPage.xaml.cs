using System.Windows;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for FastFlagCatalogPage.xaml
    /// </summary>
    public partial class FastFlagCatalogPage
    {
        private bool _initialLoad = false;

        private FastFlagCatalogViewModel _viewModel = null!;

        public FastFlagCatalogPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagCatalogViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            // re-sync with the flag store in case the raw editor changed values
            SetupViewModel();
        }
    }
}
