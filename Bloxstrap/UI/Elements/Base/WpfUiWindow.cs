using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using Windows.Win32.Foundation;
using Windows.Win32;

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

using Bloxstrap.UI.Elements.Controls;

namespace Bloxstrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        #region Drag Variables
        private bool _isManualDrag;
        private System.Drawing.Point _dragStartMousePos;
        private System.Drawing.Point _dragStartWindowPos;
        private DateTime _hitTime = DateTime.Now;
        private readonly int _dragDelay = 10;
        #endregion

        private readonly IThemeService _themeService = new ThemeService();

        /// <summary>
        /// The rain layer this window was given automatically by <see cref="EnsureRainLayer"/>,
        /// as opposed to one declared in the window's own XAML.
        /// </summary>
        private RainBackground? _autoRainLayer;

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme(bool useAcrylic = true)
        {
            const int customThemeIndex = 2; // index for CustomTheme merged dictionary

            _themeService.SetTheme(App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Dark ? ThemeType.Dark : ThemeType.Light);
            _themeService.SetSystemAccent();

            // there doesn't seem to be a way to query the name for merged dictionaries
            var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(App.Settings.Prop.Theme.GetFinal())}.xaml") };

            // The single Rainstrap background for every window and dialog. Windows bind
            // MainWindowBackgroundBrush, so defining the gradient in one place keeps it
            // consistent app-wide without each page re-implementing it. It stays
            // translucent so the window backdrop still reads through.
            this.Resources["MainWindowBackgroundBrush"] = RainstrapBackground.Create();

            //if (App.Settings.Prop.UseAcrylicBackground && useAcrylic)
            //{
            //    this.WindowStyle = WindowStyle.None;

            //    if (!AllowsTransparency)
            //        this.AllowsTransparency = true;

            //    this.ExtendsContentIntoTitleBar = true;
            //    this.WindowBackdropType = BackgroundType.Acrylic;

            //    byte opacity = App.Settings.Prop.AcrylicBackgroundOpacity;

            //    if (App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Light)
            //        this.Resources["MainWindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(opacity, 250, 250, 250));
            //    else
            //        this.Resources["MainWindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(opacity, 32, 32, 32));
            //}
            //else
            //{
            //    this.ExtendsContentIntoTitleBar = true;
            //    this.WindowBackdropType = BackgroundType.Mica;

            //    this.Resources["MainWindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            //}

            Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;

            OnApplyTheme();

#if QA_BUILD
            this.BorderBrush = System.Windows.Media.Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        /// <summary>
        /// Called after the theme resources have been swapped, so windows can repaint
        /// anything that isn't driven by a DynamicResource.
        /// </summary>
        protected virtual void OnApplyTheme()
        {
            // A layer attached by EnsureRainLayer has no code-behind of its own to refresh
            // it on a theme change, so its streak colours are rebuilt here. Windows that
            // declare their own layer refresh it from their own override.
            _autoRainLayer?.Refresh();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            EnsureRainLayer();
        }

        /// <summary>
        /// Gives the window an animated rain layer behind its content.
        ///
        /// Done here rather than in each window's XAML so that every window gets it -
        /// including any added later - and so the setting is honoured in one place. The
        /// layer is inserted as the first child of the root grid: a grid paints its own
        /// Background before its children, so the gradient still shows through and the
        /// rain sits above it but behind the window's actual content, which is the same
        /// layering the hand-written layers in the settings and about windows use.
        ///
        /// Windows that already declare a <see cref="RainBackground"/> are left alone, and
        /// a window whose content is not a grid (the offscreen context-menu host) is
        /// skipped rather than having its layout restructured underneath it.
        /// </summary>
        private void EnsureRainLayer()
        {
            if (Content is not Grid root)
                return;

            foreach (object child in root.Children)
            {
                if (child is RainBackground)
                    return;
            }

            var rain = new RainBackground();

            Grid.SetRow(rain, 0);
            Grid.SetColumn(rain, 0);
            Grid.SetRowSpan(rain, Math.Max(1, root.RowDefinitions.Count));
            Grid.SetColumnSpan(rain, Math.Max(1, root.ColumnDefinitions.Count));

            root.Children.Insert(0, rain);

            _autoRainLayer = rain;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }

        #region Acrylic Drag Logic
        // basically, the default acrylic implementation is horrible as it causes a crap ton of lag (on windows 10) when the window is moved
        // the reason afaik is due to the window being redrawn every single time it moves, which is a no no
        // so, we're going to do the window moving ourselves.
        // the drag delay is controlled by the _dragDelay variable (wow). any variable between 5 and 15 should work good.
        // 5 should not cause that much lag but i'm going to keep it at that for now
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // skip if acrylic is not on
            if (!App.Settings.Prop.UseAcrylicBackground)
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            if (e.ClickCount > 1)
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            var clickedElement = e.OriginalSource as DependencyObject;
            bool isTitleBarClick = false;

            while (clickedElement != null)
            {
                // Both button flavours must be caught: Wpf.Ui's Button does not derive
                // from System.Windows.Controls.Button.
                if (clickedElement is System.Windows.Controls.Button
                    || clickedElement is Wpf.Ui.Controls.Button)

                {
                    base.OnPreviewMouseLeftButtonDown(e);
                    return;
                }

                if (clickedElement is TitleBar)
                {
                    isTitleBarClick = true;
                    break;
                }
                clickedElement = VisualTreeHelper.GetParent(clickedElement);
            }

            if (isTitleBarClick)
            {
                _isManualDrag = true;

                PInvoke.GetCursorPos(out _dragStartMousePos);

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                PInvoke.GetWindowRect((HWND)hwnd, out RECT rect);
                _dragStartWindowPos = new System.Drawing.Point { X = rect.left, Y = rect.top };

                this.CaptureMouse();
                e.Handled = true;
                return;
            }

            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isManualDrag)
            {
                _isManualDrag = false;
                this.ReleaseMouseCapture();
                e.Handled = true;
            }
            base.OnPreviewMouseLeftButtonUp(e);
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (_isManualDrag && this.IsMouseCaptured)
            {
                if ((DateTime.Now - _hitTime).TotalMilliseconds < _dragDelay)
                    return;

                _hitTime = DateTime.Now;

                PInvoke.GetCursorPos(out System.Drawing.Point pt);

                int deltaX = pt.X - _dragStartMousePos.X;
                int deltaY = pt.Y - _dragStartMousePos.Y;

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                PInvoke.GetWindowRect((HWND)hwnd, out Windows.Win32.Foundation.RECT rect);

                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                PInvoke.MoveWindow((HWND)hwnd, _dragStartWindowPos.X + deltaX, _dragStartWindowPos.Y + deltaY, width, height, true);
            }
            base.OnPreviewMouseMove(e);
        }
        #endregion
    }
}
