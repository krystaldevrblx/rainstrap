using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

using Bloxstrap.Enums;

namespace Bloxstrap.UI.Elements.Controls
{
    /// <summary>
    /// The Rainstrap window background: a single, theme aware gradient used by every
    /// window and dialog in the application.
    ///
    /// This is deliberately one implementation rather than a brush per page. Windows
    /// bind <c>MainWindowBackgroundBrush</c> (see <see cref="Base.WpfUiWindow"/>), so
    /// defining the gradient here makes it consistent everywhere for free.
    ///
    /// The gradient stays translucent so the window's Mica/Acrylic backdrop still reads
    /// through it, and it is kept shallow enough that body text is unaffected.
    /// </summary>
    internal static class RainstrapBackground
    {
        /// <summary>
        /// Builds the global background brush for the current theme.
        /// Callers must re-invoke this after a theme change.
        /// </summary>
        public static Brush Create()
        {
            bool light = App.Settings.Prop.Theme.GetFinal() == Theme.Light;

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0.35, 1)
            };

            if (light)
            {
                // A cool wash that keeps the light theme airy without fighting the Mica.
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(38, 96, 132, 190), 0.6));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(52, 74, 104, 156), 1.0));
            }
            else
            {
                // Deep navy settling into near-black, matching the original rain backdrop
                // so the app's visual identity is unchanged.
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(150, 13, 21, 42), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(178, 8, 14, 28), 0.55));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(198, 4, 7, 16), 1.0));
            }

            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Blue-white over the navy backdrop, muted blue over the light one.
        /// </summary>
        public static Color Streak => App.Settings.Prop.Theme.GetFinal() == Theme.Light
            ? Color.FromArgb(255, 70, 102, 154)
            : Color.FromArgb(255, 198, 220, 255);

        /// <summary>
        /// Low alpha streaks need more presence to read against a bright backdrop.
        /// </summary>
        public static double OpacityScale => App.Settings.Prop.Theme.GetFinal() == Theme.Light ? 2.1 : 1.0;
    }

    /// <summary>
    /// The animated half of the Rainstrap background: a light ambient rain effect drawn
    /// straight into the element's own drawing surface. It owns no child elements,
    /// allocates nothing per frame, and is tuned to be a background detail rather than an
    /// animation in its own right.
    ///
    /// Frame pacing: the animation is driven by <see cref="CompositionTarget.Rendering"/>
    /// rather than a timer, so a redraw is only ever requested on a frame the compositor is
    /// about to present anyway. A fixed 60 Hz timestep is accumulated against the clock and
    /// at most one step is consumed per rendered frame, which gives 60 updates a second on a
    /// 60 Hz display and the same rate on a faster one - instead of the monitor rate scaling
    /// the speed of the rain.
    ///
    /// Layering note: the Rainstrap gradient lives underneath this (see
    /// <see cref="RainstrapBackground"/>). Turning the rain off therefore removes only
    /// the falling streaks and leaves the gradient, Mica and window identity intact.
    /// </summary>
    public class RainBackground : FrameworkElement
    {
        #region Tuning

        /// <summary>Three depth tiers, back to front.</summary>
        private const int TierCount = 3;

        /// <summary>Opacity variants per tier, so streaks within a tier aren't uniform.</summary>
        private const int VariantsPerTier = 4;

        /// <summary>
        /// One drop per this many square pixels. This is the main cost lever: every drop is
        /// one stroked line on an already-cached pen, so the budget is generous enough for
        /// the effect to read as weather rather than as a handful of scratches.
        /// </summary>
        private const double PixelsPerDrop = 5000.0;

        private const int MinDrops = 24;
        private const int MaxDrops = 280;

        /// <summary>Seconds of simulated time per redraw, i.e. the 60 Hz target.</summary>
        private const double StepSeconds = 1.0 / 60.0;

        /// <summary>
        /// Slack on the pacing test, as a fraction of a step. The render loop never lands on
        /// an exact multiple, so without this a 60 Hz display would drop a frame every time
        /// its callback came in a hair early and rain would visibly stutter.
        /// </summary>
        private const double StepTolerance = 0.9;

        /// <summary>
        /// Gaps longer than this are treated as a stall rather than as elapsed time. A
        /// debugger break, a window drag or a resume from sleep is not time the rain should
        /// travel through, and integrating it would teleport the whole field.
        /// </summary>
        private const double MaxFrameGapSeconds = 0.25;

        /// <summary>
        /// Coarse sample rate used when the window is being drawn without a GPU. Motion
        /// stays time-accurate, it is just sampled far less often.
        /// </summary>
        private const double LowPowerStepSeconds = 1.0 / 12.0;

        // px per second, and how much each drop may vary from it
        private static readonly double[] TierSpeeds = { 100, 170, 255 };
        private static readonly double[] TierSpeedSpread = { 55, 75, 95 };

        // streak length in pixels, and how much each drop may vary from it
        private static readonly double[] TierLengths = { 7, 14, 23 };
        private static readonly double[] TierLengthSpread = { 7, 10, 13 };

        // streak thickness in device independent pixels
        private static readonly double[] TierThickness = { 0.6, 0.75, 0.9 };

        // opacity envelope per tier, walked across the variants
        private static readonly int[] TierBaseAlpha = { 20, 32, 46 };
        private static readonly int[] TierAlphaStep = { 5, 7, 9 };

        // chance of each tier, so the field is mostly far rain with a few near streaks
        private static readonly double[] TierWeights = { 0.52, 0.33, 0.15 };

        #endregion

        private struct RainDrop
        {
            public double X;
            public double Y;
            public double Speed;
            public double Length;
            public double Drift;
            public int Pen;
        }

        private RainDrop[] _drops = Array.Empty<RainDrop>();
        private Pen[] _pens = Array.Empty<Pen>();

        /// <summary>How many drops are live; never more than <see cref="_drops"/>.</summary>
        private int _dropCount;

        private readonly Random _random = new();

        private Window? _window;
        private bool _updatingVisibility;

        /// <summary>Set while subscribed to the composition rendering event.</summary>
        private bool _renderingAttached;

        /// <summary>Time banked toward the next redraw, in seconds.</summary>
        private double _accumulator;

        /// <summary>Whether the user has the rain switched on at all.</summary>
        private bool _rainWanted = true;

        private double _lastStamp;
        private double _stepSeconds = StepSeconds;

        public RainBackground()
        {
            IsHitTestVisible = false;
            Focusable = false;
            SnapsToDevicePixels = false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Visibility = Visibility.Collapsed;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        /// <summary>
        /// Rebuilds the streak colours for the current theme and setting, then shows or
        /// hides the layer to match. Only the rain is toggled here - the gradient is
        /// owned by <see cref="RainstrapBackground"/> and is unaffected.
        /// </summary>
        public void Refresh()
        {
            RebuildPens();

            // apply the setting first, so a freshly hidden layer detaches before the
            // composition event has a chance to ask it for another frame
            ApplyEnabled();

            RebuildDrops();

            _accumulator = 0;
            _lastStamp = Now;

            if (IsVisible)
                InvalidateVisual();

            UpdatePlayback();
        }

        #region Lifetime

        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_window == null && Window.GetWindow(this) is Window window)
            {
                _window = window;
                _window.StateChanged += OnWindowStateChanged;
                _window.Activated += OnWindowActivationChanged;
                _window.Deactivated += OnWindowActivationChanged;
            }

            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Stop();

            if (_window != null)
            {
                _window.StateChanged -= OnWindowStateChanged;
                _window.Activated -= OnWindowActivationChanged;
                _window.Deactivated -= OnWindowActivationChanged;
                _window = null;
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // ApplyEnabled flips Visibility itself; that is not a reason to re-arm playback
            // halfway through, so it defers to the UpdatePlayback at the end of Refresh.
            if (_updatingVisibility)
                return;

            UpdatePlayback();
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
            => UpdatePlayback();

        private void OnWindowActivationChanged(object? sender, EventArgs e)
            => UpdatePlayback();

        /// <summary>
        /// Software rendering has no headroom to spare, so the rain drops to a sparse
        /// sample rather than fighting the rest of the UI for frames.
        /// </summary>
        private static bool IsLowPower =>
            App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active;

        private void ApplyEnabled()
        {
            if (_updatingVisibility)
                return;

            _updatingVisibility = true;

            try
            {
                // Only the animated layer follows this setting. The gradient underneath
                // is part of the app's background and always stays.
                _rainWanted = App.Settings.Prop.RainBackgroundEnabled;

                Visibility = _rainWanted ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _updatingVisibility = false;
            }
        }

        private bool ShouldRun => _rainWanted
            && IsVisible
            && _window?.WindowState != System.Windows.WindowState.Minimized;

        /// <summary>
        /// While the window is in the foreground the rain animates at 60 Hz. Playback
        /// detaches only when there is genuinely nothing to draw - the setting is off, the
        /// layer is hidden, or the window is minimised. Being merely occluded needs no
        /// special case: WPF stops delivering composition frames for an offscreen window on
        /// its own, so the loop goes quiet by itself and the banked time is discarded on
        /// return rather than being replayed as a jump.
        /// </summary>
        private void UpdatePlayback()
        {
            if (ShouldRun)
            {
                _stepSeconds = IsLowPower ? LowPowerStepSeconds : StepSeconds;

                // never integrate time spent hidden, or the rain jumps on return
                _accumulator = 0;
                _lastStamp = Now;

                Attach();
            }
            else
            {
                Stop();
            }
        }

        private void Attach()
        {
            if (_renderingAttached)
                return;

            CompositionTarget.Rendering += OnRendering;
            _renderingAttached = true;
        }

        private void Stop()
        {
            if (!_renderingAttached)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _renderingAttached = false;
        }

        #endregion

        #region Construction

        private void RebuildPens()
        {
            if (_pens.Length != TierCount * VariantsPerTier)
                _pens = new Pen[TierCount * VariantsPerTier];

            Color streak = RainstrapBackground.Streak;
            double scale = RainstrapBackground.OpacityScale;

            for (int tier = 0; tier < TierCount; tier++)
            {
                for (int variant = 0; variant < VariantsPerTier; variant++)
                {
                    int alpha = (int)Math.Round((TierBaseAlpha[tier] + TierAlphaStep[tier] * variant) * scale);

                    var brush = new SolidColorBrush(
                        Color.FromArgb((byte)Math.Clamp(alpha, 5, 255), streak.R, streak.G, streak.B));
                    brush.Freeze();

                    var pen = new Pen(brush, TierThickness[tier])
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    };
                    pen.Freeze();

                    _pens[tier * VariantsPerTier + variant] = pen;
                }
            }
        }

        /// <summary>
        /// Sizes the field for the current surface and re-seeds it.
        ///
        /// The backing array only ever grows, so a window drag (which resizes every frame)
        /// resamples drops instead of allocating a new array each time.
        /// </summary>
        private void RebuildDrops()
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0)
            {
                // stale drops would be drawn against the new, wrong geometry
                _dropCount = 0;
                return;
            }

            int count = (int)(width * height / PixelsPerDrop);

            if (IsLowPower)
                count /= 3;

            count = Math.Clamp(count, MinDrops, MaxDrops);

            if (_drops.Length < count)
            {
                var grown = new RainDrop[count];
                Array.Copy(_drops, grown, _drops.Length);
                _drops = grown;
            }

            for (int i = 0; i < count; i++)
            {
                int tier = PickTier();

                // spread the initial positions over the full column so the field is
                // already populated on the first frame rather than filling in
                _drops[i] = new RainDrop
                {
                    X = _random.NextDouble() * width,
                    Y = (_random.NextDouble() * 2.0 - 1.0) * height,
                    Speed = TierSpeeds[tier] + _random.NextDouble() * TierSpeedSpread[tier],
                    Length = TierLengths[tier] + _random.NextDouble() * TierLengthSpread[tier],
                    Drift = 0.05 + _random.NextDouble() * 0.13,
                    Pen = tier * VariantsPerTier + _random.Next(VariantsPerTier)
                };
            }

            _dropCount = count;
        }

        private int PickTier()
        {
            double roll = _random.NextDouble();

            if (roll < TierWeights[0])
                return 0;

            return roll < TierWeights[0] + TierWeights[1] ? 1 : 2;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Called once per frame the compositor is about to present.
        ///
        /// Pacing is what keeps this at 60 fps rather than at the monitor's refresh rate.
        /// Time is banked as it passes and a redraw is requested once a 60 Hz step has
        /// accumulated; the drops then advance by everything banked, not by one step. That
        /// second part is what makes fall speed identical on every display:
        ///
        ///   60 Hz   one tick per step          -> 60 redraws/s, one 16.7 ms step each
        ///   144 Hz  three ticks per step       -> 48 redraws/s, one 20.8 ms step each
        ///   30 Hz   one tick covers two steps -> 30 redraws/s, one 33.3 ms step each
        ///
        /// Gating on a single step's worth of gap instead (rather than on the accumulated
        /// total) looks equivalent on a 60 Hz display but silently freezes the animation
        /// above 66 Hz, where no individual gap is ever long enough to trip the test.
        /// </summary>
        private void OnRendering(object? sender, EventArgs e)
        {
            if (!ShouldRun)
            {
                Stop();
                return;
            }

            int count = _dropCount;

            if (count == 0)
                return;

            double now = Now;
            double gap = now - _lastStamp;
            _lastStamp = now;

            // a backwards clock must not rewind the field
            if (gap <= 0)
                return;

            // a stall is dropped rather than integrated, or the whole field teleports
            if (gap > MaxFrameGapSeconds)
            {
                _accumulator = 0;
                return;
            }

            _accumulator += gap;

            // not yet a 60 Hz step's worth of time
            if (_accumulator < _stepSeconds * StepTolerance)
                return;

            double delta = _accumulator;
            _accumulator = 0;

            if (delta > MaxFrameGapSeconds)
                delta = MaxFrameGapSeconds;

            Advance(count, delta);

            InvalidateVisual();
        }

        private void Advance(int count, double delta)
        {
            double height = ActualHeight;
            double width = ActualWidth;

            for (int i = 0; i < count; i++)
            {
                ref RainDrop drop = ref _drops[i];

                drop.Y += drop.Speed * delta;

                if (drop.Y > height)
                {
                    // wrap to the top, and resample the column so the field never
                    // settles into visible vertical lanes
                    drop.Y -= height + drop.Length;
                    drop.X = _random.NextDouble() * width;
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            int count = _dropCount;
            Pen[] pens = _pens;

            double width = ActualWidth;
            double height = ActualHeight;

            if (count == 0 || pens.Length == 0 || width <= 0 || height <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                ref RainDrop drop = ref _drops[i];

                // drop.Y is the leading end of the streak, the tail trails above it
                if (drop.Y < 0 || drop.Y - drop.Length > height)
                    continue;

                dc.DrawLine(
                    pens[drop.Pen],
                    new Point(drop.X + drop.Length * drop.Drift, drop.Y - drop.Length),
                    new Point(drop.X, drop.Y));
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);

            RebuildDrops();
            InvalidateVisual();
        }

        #endregion
    }
}
