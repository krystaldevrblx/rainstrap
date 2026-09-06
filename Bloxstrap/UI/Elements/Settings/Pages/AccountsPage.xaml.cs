using System.Windows;

using Bloxstrap.Plugins;
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
            if (!_initialLoad)
            {
                _initialLoad = true;
            }

            if (App.PluginManager is null ||
                !App.PluginManager.Plugins.TryGetValue("rainstrap.accountmanager", out var plugin) ||
                plugin is not AccountManagerPlugin accountPlugin)
            {
                DataContext = null;
                return;
            }

            DataContext = new AccountsViewModel(accountPlugin);
        }
    }
}
