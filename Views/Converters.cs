using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SecureNotesWin.Helpers;

namespace SecureNotesWin.Views
{
    public static class Converters
    {
        public static readonly UnixMsToDateTimeConverter   UnixMsToDateTime = new();
        public static readonly ColorStringToBrushConverter ColorToBrush     = new();
        public static readonly BodyPreviewConverter         BodyPreview      = new();
    }

    /// <summary>
    /// Converts a raw markdown note body to clean plain text for the note-list
    /// preview — no image refs, no markdown syntax, no HTML tags.
    /// </summary>
    public class BodyPreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string body ? NoteBodyHelper.StripForListPreview(body) : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class UnixMsToDateTimeConverter : IValueConverter
    {
        // Returns a human-readable local date string from a Unix-ms timestamp.
        // Pass ConverterParameter="long" to include the time component.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not long ms || ms <= 0) return string.Empty;
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
            var includTime = parameter is string p && p == "long";
            return includTime
                ? dt.ToString("d MMM yyyy  HH:mm", culture)
                : dt.ToString("d MMM yyyy", culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ColorStringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try
                {
                    var color = (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
            return new SolidColorBrush(Colors.Orange);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Rasterises an emoji string into a <see cref="BitmapSource"/> using GDI+
    /// with the "Segoe UI Emoji" font, which supports full-colour (COLR/CPAL)
    /// glyphs on Windows 10 / 11.  The result is cached by emoji string so the
    /// same glyph is only rendered once per session.
    /// </summary>
    public class EmojiImageConverter : IValueConverter
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>
            _cache = new();

        // ConverterParameter can override the pixel size; default is 20 px.
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var emoji = value as string;
            if (string.IsNullOrWhiteSpace(emoji)) return null;

            int size = 20;
            if (parameter is string ps && int.TryParse(ps, out var parsed)) size = parsed;

            var key = $"{emoji}_{size}";
            return _cache.GetOrAdd(key, _ => Render(emoji, size));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static BitmapSource Render(string emoji, int size)
        {
            // Render at 2× then downscale for crisp results at small sizes.
            int hi = size * 2;

            using var bmp = new Bitmap(hi, hi, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                // "Segoe UI Emoji" renders full-colour glyphs via GDI+ on Win10/11.
                using var font = new System.Drawing.Font("Segoe UI Emoji", hi * 0.72f,
                    System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(System.Drawing.Color.White);

                var fmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(emoji, font, brush,
                    new RectangleF(0, 0, hi, hi), fmt);
            }

            // Lock bits and copy to a WPF BitmapSource.
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, hi, hi),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var wpfBmp = BitmapSource.Create(
                hi, hi, 96, 96,
                PixelFormats.Bgra32, null,
                bmpData.Scan0,
                bmpData.Stride * hi,
                bmpData.Stride);

            bmp.UnlockBits(bmpData);
            wpfBmp.Freeze();

            // Downscale to target size for a sharper result.
            var scaled = new TransformedBitmap(wpfBmp,
                new ScaleTransform((double)size / hi, (double)size / hi));
            scaled.Freeze();
            return scaled;
        }
    }
}
