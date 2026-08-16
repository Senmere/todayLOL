using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TodayLOL.Services;

namespace TodayLOL.Views
{
    public partial class CaptureWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isSelecting;
        private Bitmap? _screenCapture;

        public event EventHandler<Bitmap?>? CaptureCompleted;

        public CaptureWindow(Bitmap screenCapture)
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            KeyDown += Window_KeyDown;

            _screenCapture = screenCapture;

            var bitmapSource = CaptureService.ToBitmapSource(screenCapture);
            BgImage.Source = bitmapSource;
            BgImage.Width = screenCapture.Width;
            BgImage.Height = screenCapture.Height;

            Overlay.Width = screenCapture.Width;
            Overlay.Height = screenCapture.Height;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isSelecting = true;
                _startPoint = e.GetPosition(this);
                SelectRect.Visibility = Visibility.Visible;
                SelectRectInner.Visibility = Visibility.Visible;
                CaptureMouse();
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                _screenCapture?.Dispose();
                CaptureCompleted?.Invoke(this, null);
                Close();
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isSelecting)
            {
                var currentPoint = e.GetPosition(this);
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                Canvas.SetLeft(SelectRect, x);
                Canvas.SetTop(SelectRect, y);
                SelectRect.Width = width;
                SelectRect.Height = height;

                Canvas.SetLeft(SelectRectInner, x);
                Canvas.SetTop(SelectRectInner, y);
                SelectRectInner.Width = width;
                SelectRectInner.Height = height;

                SizeText.Text = $"{(int)width} × {(int)height}";
                Canvas.SetLeft(SizeText, x);
                Canvas.SetTop(SizeText, y - 28);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Released)
            {
                _isSelecting = false;
                ReleaseMouseCapture();

                var currentPoint = e.GetPosition(this);
                var x = (int)Math.Min(_startPoint.X, currentPoint.X);
                var y = (int)Math.Min(_startPoint.Y, currentPoint.Y);
                var width = (int)Math.Abs(currentPoint.X - _startPoint.X);
                var height = (int)Math.Abs(currentPoint.Y - _startPoint.Y);

                Bitmap? croppedBitmap = null;

                if (width > 5 && height > 5 && _screenCapture != null)
                {
                    try
                    {
                        var rect = new Rectangle(x, y, width, height);
                        croppedBitmap = _screenCapture.Clone(rect, _screenCapture.PixelFormat);
                    }
                    catch { }
                }

                _screenCapture?.Dispose();
                CaptureCompleted?.Invoke(this, croppedBitmap);
                Close();
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _screenCapture?.Dispose();
                CaptureCompleted?.Invoke(this, null);
                Close();
            }
        }
    }
}