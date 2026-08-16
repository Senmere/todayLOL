using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sd = System.Drawing;
using Sd2d = System.Drawing.Drawing2D;

namespace TodayLOL.Services
{
    public static class IconHelper
    {
        private static string? _customIconPath;
        private static Sd.Icon? _trayIcon;
        private static ImageSource? _windowIcon;

        public static void Init()
        {
            var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "appicon.png");
            if (File.Exists(assetsPath))
            {
                _customIconPath = assetsPath;
            }
        }

        public static Sd.Icon GetTrayIcon()
        {
            if (_trayIcon != null) return _trayIcon;

            if (!string.IsNullOrEmpty(_customIconPath))
            {
                try
                {
                    using var bmp = new Sd.Bitmap(_customIconPath);
                    _trayIcon = IconFromBitmap(bmp, 32);
                    return _trayIcon;
                }
                catch { }
            }

            _trayIcon = IconFromBitmap(CreateDefaultBitmap(64), 32);
            return _trayIcon;
        }

        public static ImageSource WindowIcon => GetWindowIcon();

        public static ImageSource GetWindowIcon()
        {
            if (_windowIcon != null) return _windowIcon;

            if (!string.IsNullOrEmpty(_customIconPath))
            {
                try
                {
                    var uri = new Uri(_customIconPath, UriKind.Absolute);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.DecodePixelWidth = 64;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    _windowIcon = bmp;
                    return _windowIcon;
                }
                catch { }
            }

            _windowIcon = BitmapSourceFromGdiBitmap(CreateDefaultBitmap(64));
            return _windowIcon;
        }

        private static Sd.Bitmap CreateDefaultBitmap(int size)
        {
            var bmp = new Sd.Bitmap(size, size);
            using (var g = Sd.Graphics.FromImage(bmp))
            {
                g.InterpolationMode = Sd2d.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = Sd2d.PixelOffsetMode.Half;
                g.Clear(Sd.Color.Transparent);

                int scale = size / 16;
                if (scale < 1) scale = 1;

                int px(int v) => v * scale;

                // 深蓝背景方块（像素风格圆角）
                void FillPixel(int x, int y, Sd.Color c)
                {
                    g.FillRectangle(new Sd.SolidBrush(c), px(x), px(y), scale, scale);
                }

                // 16x16 像素画布，背景填充
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        FillPixel(x, y, Sd.Color.FromArgb(40, 60, 100));
                    }
                }

                // 边框（浅蓝灰色像素边）
                Sd.Color border = Sd.Color.FromArgb(120, 140, 180);
                for (int x = 0; x < 16; x++)
                {
                    FillPixel(x, 0, border);
                    FillPixel(x, 15, border);
                }
                for (int y = 1; y < 15; y++)
                {
                    FillPixel(0, y, border);
                    FillPixel(15, y, border);
                }

                // 像素风格 ;) —— 横着排列
                // 分号 ; 的点在 (4,3)
                FillPixel(4, 3, Sd.Color.White);
                // 分号 ; 的逗号/尾巴从 (4,5) 到 (4,7) 偏左下
                FillPixel(4, 5, Sd.Color.White);
                FillPixel(4, 6, Sd.Color.White);
                FillPixel(3, 7, Sd.Color.White);

                // 右括号 ) —— 从 (8,2) 开始画半圆
                // 上弧
                FillPixel(8, 2, Sd.Color.White);
                FillPixel(9, 2, Sd.Color.White);
                FillPixel(10, 3, Sd.Color.White);
                // 右竖
                FillPixel(11, 4, Sd.Color.White);
                FillPixel(11, 5, Sd.Color.White);
                FillPixel(11, 6, Sd.Color.White);
                FillPixel(11, 7, Sd.Color.White);
                FillPixel(11, 8, Sd.Color.White);
                FillPixel(11, 9, Sd.Color.White);
                FillPixel(11, 10, Sd.Color.White);
                // 下弧
                FillPixel(10, 11, Sd.Color.White);
                FillPixel(9, 12, Sd.Color.White);
                FillPixel(8, 12, Sd.Color.White);
            }
            return bmp;
        }

        private static Sd2d.GraphicsPath RoundedRect(Sd.RectangleF rect, float radius)
        {
            var path = new Sd2d.GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Sd.Icon IconFromBitmap(Sd.Bitmap source, int targetSize)
        {
            var bmp = new Sd.Bitmap(targetSize, targetSize);
            using (var g = Sd.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = Sd2d.SmoothingMode.AntiAlias;
                g.InterpolationMode = Sd2d.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, targetSize, targetSize);
            }
            return Sd.Icon.FromHandle(bmp.GetHicon());
        }

        private static BitmapSource BitmapSourceFromGdiBitmap(Sd.Bitmap source)
        {
            var hBitmap = source.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
