using System.Windows;

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
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new MultiInstanceViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            // refresh the instance list every time the page is revisited
            _viewModel.Refresh();
        }
    }
}
