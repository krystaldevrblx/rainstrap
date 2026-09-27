using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Bloxstrap.UI.Converters
{
    /// <summary>
    /// Turns a remote image URL into an <see cref="ImageSource"/> that decodes at the size
    /// it will actually be drawn at.
    ///
    /// Binding a URL string straight to <c>Image.Source</c> makes WPF decode the full
    /// original - a Roblox thumbnail is 420x420, which is around 700 KB of bitmap for a
    /// 32 or 44 pixel box. A list of those is tens of megabytes of pixel data for something
    /// the user cannot resolve, and it is decoded on the UI thread.
    ///
    /// <c>DecodePixelWidth</c> is only settable on the <see cref="BitmapImage"/>, not on the
    /// <c>Image</c> element, hence the converter.
    ///
    /// Instances are cached per (url, size). A virtualizing list hands a recycled row a new
    /// data context, and without the cache every scroll would start the download again.
    /// </summary>
    class RemoteImageConverter : IValueConverter
    {
        /// <summary>Size used when the binding supplies no usable parameter.</summary>
        private const int DefaultDecodeWidth = 96;

        private const int MaxCacheEntries = 256;

        private static readonly Dictionary<(string Url, int Width), BitmapImage> Cache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return Binding.DoNothing;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Binding.DoNothing;

            int width = ParseWidth(parameter);

            return GetOrCreate(uri, width);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static int ParseWidth(object? parameter)
        {
            if (parameter is int direct)
                return Math.Clamp(direct, 1, 1024);

            if (parameter is string text
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return Math.Clamp(parsed, 1, 1024);
            }

            return DefaultDecodeWidth;
        }

        private static BitmapImage GetOrCreate(Uri uri, int width)
        {
            var key = (uri.AbsoluteUri, width);

            lock (Cache)
            {
                if (Cache.TryGetValue(key, out var cached))
                    return cached;
            }

            var image = new BitmapImage();
            image.BeginInit();

            // OnDemand keeps the download off the UI thread; DecodePixelWidth still applies,
            // so the bitmap that lands in memory is the small one.
            image.CacheOption = BitmapCacheOption.OnDemand;
            image.DecodePixelWidth = width;
            image.UriSource = uri;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            image.EndInit();

            // A frozen source can be shared across rows and across dispatcher frames without
            // WPF having to guard it.
            image.Freeze();

            lock (Cache)
            {
                // Bounded so a long session browsing many games cannot grow this forever.
                if (Cache.Count >= MaxCacheEntries)
                    Cache.Clear();

                Cache[key] = image;
            }

            return image;
        }
    }
}
