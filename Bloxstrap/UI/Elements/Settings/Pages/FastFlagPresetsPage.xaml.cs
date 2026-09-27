using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

using Bloxstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for FastFlagPresetsPage.xaml
    /// </summary>
    public partial class FastFlagPresetsPage
    {
        private bool _initialLoad = false;

        private FastFlagPresetsViewModel _viewModel = null!;

        public FastFlagPresetsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagPresetsViewModel();
            _viewModel.LoadPresets();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;

            // Presets write straight into FastFlagManager, so the rest of the app picks
            // the change up through the same save path as hand-edited flags.
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();

            DataContext = _viewModel;
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                window.Navigate(typeof(FastFlagEditorPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Flag values can change on the editor page while this one is cached, so
            // rebuild the applied/partially-applied state every time it comes into view.
            if (_initialLoad)
                SetupViewModel();

            _initialLoad = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });

            e.Handled = true;
        }
    }
}
