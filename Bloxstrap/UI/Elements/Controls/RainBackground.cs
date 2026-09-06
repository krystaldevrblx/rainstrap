using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Bloxstrap.UI.Elements.Controls
{
    public class RainBackground : Canvas, IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly List<RainDrop> _drops = new();
        private bool _isActive;
        private bool _disposed;

        private const int DropCount = 60;
        private const double MinSpeed = 4.0;
        private const double MaxSpeed = 12.0;
        private const double MinLength = 8.0;
        private const double MaxLength = 22.0;
        private const double MinOpacity = 0.06;
        private const double MaxOpacity = 0.18;

        private static readonly Random Random = new();
        private static readonly SolidColorBrush DropBrush = new(Color.FromArgb(255, 200, 210, 230));

        public RainBackground()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            if (_isActive || _disposed)
                return;

            _isActive = true;
            EnsureDrops();
            _timer.Start();
        }

        public void Stop()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _timer.Stop();
        }

        private void EnsureDrops()
        {
            if (_drops.Count > 0)
                return;

            double w = ActualWidth > 0 ? ActualWidth : 800;
            double h = ActualHeight > 0 ? ActualHeight : 600;

            for (int i = 0; i < DropCount; i++)
                _drops.Add(CreateDrop(w, h, randomizeY: true));
        }

        private RainDrop CreateDrop(double w, double h, bool randomizeY)
        {
            double x = Random.NextDouble() * w;
            double y = randomizeY ? Random.NextDouble() * h : -MaxLength;
            double speed = MinSpeed + Random.NextDouble() * (MaxSpeed - MinSpeed);
            double length = MinLength + Random.NextDouble() * (MaxLength - MinLength);
            double opacity = MinOpacity + Random.NextDouble() * (MaxOpacity - MinOpacity);

            var line = new Line
            {
                X1 = x,
                Y1 = y,
                X2 = x + 0.5,
                Y2 = y + length,
                Stroke = DropBrush,
                StrokeThickness = 1,
                Opacity = opacity,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            line.IsHitTestVisible = false;

            var drop = new RainDrop
            {
                Line = line,
                Speed = speed,
                Length = length
            };

            Children.Add(line);
            return drop;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            double h = ActualHeight;
            double w = ActualWidth;

            for (int i = 0; i < _drops.Count; i++)
            {
                var drop = _drops[i];
                var line = drop.Line;

                double newY1 = line.Y1 + drop.Speed;
                double newY2 = newY1 + drop.Length;

                if (newY1 > h)
                {
                    double x = Random.NextDouble() * w;
                    line.X1 = x;
                    line.X2 = x + 0.5;
                    line.Y1 = -drop.Length;
                    line.Y2 = 0;
                }
                else
                {
                    line.Y1 = newY1;
                    line.Y2 = newY2;
                }
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_isActive)
            {
                foreach (var drop in _drops)
                {
                    if (drop.Line.X1 > sizeInfo.NewSize.Width)
                    {
                        double x = Random.NextDouble() * sizeInfo.NewSize.Width;
                        drop.Line.X1 = x;
                        drop.Line.X2 = x + 0.5;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer.Stop();

            foreach (var drop in _drops)
                Children.Remove(drop.Line);

            _drops.Clear();
            GC.SuppressFinalize(this);
        }

        private class RainDrop
        {
            public Line Line { get; set; } = null!;
            public double Speed { get; set; }
            public double Length { get; set; }
        }
    }
}
