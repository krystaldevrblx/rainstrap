using System.Windows;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for AccountsPage.xaml
    /// </summary>
    public partial class AccountsPage
    {
        private bool _initialLoad = false;

        public AccountsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // refresh on every load so the list stays in sync with state changes
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            DataContext = new AccountsViewModel();
        }
    }
}
