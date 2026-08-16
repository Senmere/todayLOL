using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TodayLOL.Data;
using TodayLOL.Models;
using TodayLOL.Views;

namespace TodayLOL.Services
{
    public static class CaptureService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        private const int SRCCOPY = 0x00CC0020;

        public static void StartCapture()
        {
            StartCapture(null);
        }

        public static async void StartCapture(Action<Bitmap?>? callback)
        {
            var screenWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            var screenHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;

            var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            var wasVisible = mainWindow?.IsVisible ?? false;
            if (mainWindow != null && wasVisible)
            {
                mainWindow.Hide();
                await Task.Delay(150);
            }

            var screenCapture = CaptureScreen();

            var captureWindow = new CaptureWindow(screenCapture)
            {
                Width = screenWidth,
                Height = screenHeight,
                Left = 0,
                Top = 0
            };

            captureWindow.CaptureCompleted += (s, bitmap) =>
            {
                if (mainWindow != null && wasVisible)
                {
                    mainWindow.Show();
                }

                if (callback != null)
                {
                    callback(bitmap);
                }
                else if (bitmap != null)
                {
                    if (Models.Settings.Instance.AutoEditAfterCapture)
                    {
                        var editor = new EditorWindow(bitmap);
                        editor.Show();
                    }
                    else
                    {
                        SaveDirectly(bitmap);
                    }
                }
            };

            captureWindow.Show();
        }

        public static Bitmap CaptureScreen()
        {
            var screenWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            var screenHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;
            var width = screenWidth;
            var height = screenHeight;

            var hDesk = GetDC(IntPtr.Zero);
            var hDC = CreateCompatibleDC(hDesk);
            var hBmp = CreateCompatibleBitmap(hDesk, width, height);
            var hOld = SelectObject(hDC, hBmp);

            BitBlt(hDC, 0, 0, width, height, hDesk, 0, 0, SRCCOPY);

            SelectObject(hDC, hOld);
            DeleteDC(hDC);
            ReleaseDC(IntPtr.Zero, hDesk);

            var bitmap = System.Drawing.Image.FromHbitmap(hBmp);
            DeleteObject(hBmp);

            return bitmap;
        }

        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private static void SaveDirectly(Bitmap bitmap)
        {
            var settings = Models.Settings.Instance;
            var now = DateTime.Now;
            var fileName = $"难绷_{now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(settings.SavePath, fileName);

            var watermarked = AddWatermark(bitmap, now, settings.WatermarkPosition);
            watermarked.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            SaveRecord(filePath, now, settings.WatermarkPosition);
        }

        public static void SaveWithDescription(Bitmap bitmap, string description)
        {
            var settings = Models.Settings.Instance;
            var now = DateTime.Now;
            var fileName = $"难绷_{now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(settings.SavePath, fileName);

            var watermarked = AddWatermark(bitmap, now, settings.WatermarkPosition);
            watermarked.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            SaveRecord(filePath, now, settings.WatermarkPosition, description);
        }

        public static Bitmap AddWatermark(Bitmap bitmap, DateTime time, string position)
        {
            var result = new Bitmap(bitmap.Width, bitmap.Height);
            using var g = Graphics.FromImage(result);
            g.DrawImage(bitmap, 0, 0);

            var watermarkText = time.ToString("yyyy-MM-dd HH:mm");
            using var font = new Font("Microsoft YaHei", 12, System.Drawing.FontStyle.Regular);
            var brush = Brushes.White;
            var shadowBrush = Brushes.Black;

            var textSize = g.MeasureString(watermarkText, font);
            var padding = 10;
            var pos = GetWatermarkPosition(bitmap.Width, bitmap.Height, (int)textSize.Width, (int)textSize.Height, padding, position);

            // 阴影效果
            g.DrawString(watermarkText, font, shadowBrush, pos.X + 1, pos.Y + 1);
            g.DrawString(watermarkText, font, brush, pos.X, pos.Y);

            return result;
        }

        private static System.Drawing.Point GetWatermarkPosition(int imgWidth, int imgHeight, int textWidth, int textHeight, int padding, string position)
        {
            return position switch
            {
                "TopLeft" => new System.Drawing.Point(padding, padding),
                "TopRight" => new System.Drawing.Point(imgWidth - textWidth - padding, padding),
                "BottomLeft" => new System.Drawing.Point(padding, imgHeight - textHeight - padding),
                _ => new System.Drawing.Point(imgWidth - textWidth - padding, imgHeight - textHeight - padding)
            };
        }

        private static void SaveRecord(string filePath, DateTime createTime, string watermarkPosition, string description = "")
        {
            using var db = new Data.RecordDbContext();
            var record = new Models.Record
            {
                FilePath = filePath,
                Description = description,
                CreateTime = createTime,
                WatermarkPosition = watermarkPosition
            };
            db.Records.Add(record);
            db.SaveChanges();
        }
    }
}