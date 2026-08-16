using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TodayLOL.Data;
using TodayLOL.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using RadioButton = System.Windows.Controls.RadioButton;
using Rectangle = System.Windows.Shapes.Rectangle;
using MessageBox = System.Windows.MessageBox;

namespace TodayLOL.Views
{
    public partial class EditorWindow : Window
    {
        private Bitmap _originalBitmap;
        private BitmapSource _bitmapSource;
        private string _currentTool = "Pen";
        private Brush _currentColor = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
        private bool _isDrawing;
        private System.Windows.Point _startPoint;

        private ObservableCollection<DrawingElement> _drawings = new();
        private Stack<DrawingElement> _undoStack = new();
        private Stack<DrawingElement> _redoStack = new();

        private FrameworkElement? _previewElement;
        private Polyline? _currentPolyline;

        // 裁剪相关
        private Rectangle? _cropRect;
        private bool _isCropSelecting;
        private System.Windows.Point _cropStart;

        // 移动相关
        private FrameworkElement? _movingElement;
        private System.Windows.Point _moveStartPoint;

        // 缩放相关
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.25;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;

        public EditorWindow(Bitmap bitmap)
        {
            InitializeComponent();
            _originalBitmap = bitmap;
            _bitmapSource = CaptureService.ToBitmapSource(bitmap);

            InitializeCanvas();
            AddImageToCanvas();
        }

        private void InitializeCanvas()
        {
            ImageCanvas.Width = _bitmapSource.Width;
            ImageCanvas.Height = _bitmapSource.Height;
            CropOverlay.Width = _bitmapSource.Width;
            CropOverlay.Height = _bitmapSource.Height;
            CanvasContainer.Width = _bitmapSource.Width;
            CanvasContainer.Height = _bitmapSource.Height;
        }

        private void AddImageToCanvas()
        {
            var image = new System.Windows.Controls.Image
            {
                Source = _bitmapSource,
                Width = _bitmapSource.Width,
                Height = _bitmapSource.Height
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            image.Tag = "BackgroundImage";
            ImageCanvas.Children.Add(image);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tool)
            {
                _currentTool = tool;

                if (tool == "Crop")
                {
                    CropOverlay.Visibility = Visibility.Visible;
                    ImageCanvas.IsEnabled = false;
                }
                else if (tool == "Stitch")
                {
                    CropOverlay.Visibility = Visibility.Collapsed;
                    ImageCanvas.IsEnabled = true;
                    StartStitchCapture();
                }
                else
                {
                    CancelCropMode();
                }
            }
        }

        private void CancelCropMode()
        {
            CropOverlay.Visibility = Visibility.Collapsed;
            ImageCanvas.IsEnabled = true;
            ApplyCropBtn.Visibility = Visibility.Collapsed;
            CancelCropBtn.Visibility = Visibility.Collapsed;

            if (_cropRect != null)
            {
                CropOverlay.Children.Remove(_cropRect);
                _cropRect = null;
            }
        }

        private void StartStitchCapture()
        {
            CaptureService.StartCapture(stitchBitmap =>
            {
                if (stitchBitmap != null)
                {
                    // 将新截图作为独立元素添加到画布上，而不是合并到底层图片
                    var newImageSource = CaptureService.ToBitmapSource(stitchBitmap);
                    var newImage = new System.Windows.Controls.Image
                    {
                        Source = newImageSource,
                        Width = newImageSource.Width,
                        Height = newImageSource.Height
                    };
                    // 放在底层图片下方
                    var bottomImage = ImageCanvas.Children.Cast<FrameworkElement>()
                        .FirstOrDefault(el => el.Tag?.ToString() == "BackgroundImage");
                    if (bottomImage != null)
                    {
                        Canvas.SetLeft(newImage, (ImageCanvas.Width - newImage.Width) / 2);
                        Canvas.SetTop(newImage, Canvas.GetTop(bottomImage) + bottomImage.ActualHeight);
                    }
                    else
                    {
                        Canvas.SetLeft(newImage, 0);
                        Canvas.SetTop(newImage, 0);
                    }

                    ImageCanvas.Children.Add(newImage);
                    ImageCanvas.Width = Math.Max(ImageCanvas.Width, newImage.Width);
                    ImageCanvas.Height = Math.Max(ImageCanvas.Height, Canvas.GetTop(newImage) + newImage.Height);
                    CanvasContainer.Width = ImageCanvas.Width;
                    CanvasContainer.Height = ImageCanvas.Height;

                    var element = new DrawingElement { Element = newImage, Type = "StitchImage" };
                    _drawings.Add(element);
                    _undoStack.Push(element);
                    _redoStack.Clear();

                    MessageBox.Show("拼接成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                ToolPen.IsChecked = true;
            });
        }

        private void Color_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string hex)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                _currentColor = new SolidColorBrush(color);

                foreach (var child in ColorPanel.Children)
                {
                    if (child is Border b)
                    {
                        b.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    }
                }
                border.BorderBrush = System.Windows.Media.Brushes.Black;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(ImageCanvas);

            if (_currentTool == "Move")
            {
                var hitTest = VisualTreeHelper.HitTest(ImageCanvas, pos);
                if (hitTest != null && hitTest.VisualHit is FrameworkElement element &&
                    element != ImageCanvas)
                {
                    // 向上查找实际的元素
                    while (element.Parent is FrameworkElement parent && parent != ImageCanvas)
                    {
                        element = parent;
                    }
                    _movingElement = element;
                    _moveStartPoint = pos;
                    ImageCanvas.CaptureMouse();
                }
                return;
            }

            if (_currentTool == "Text")
            {
                StartTextInput(pos);
                return;
            }

            _isDrawing = true;
            _startPoint = pos;
            ImageCanvas.CaptureMouse();

            if (_currentTool == "Pen")
            {
                _currentPolyline = new Polyline
                {
                    Stroke = _currentColor,
                    StrokeThickness = GetSelectedStrokeSize(),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                _currentPolyline.Points.Add(pos);
                Canvas.SetLeft(_currentPolyline, 0);
                Canvas.SetTop(_currentPolyline, 0);
                ImageCanvas.Children.Add(_currentPolyline);
            }
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_movingElement != null)
            {
                var currentPoint = e.GetPosition(ImageCanvas);
                var deltaX = currentPoint.X - _moveStartPoint.X;
                var deltaY = currentPoint.Y - _moveStartPoint.Y;

                var currentLeft = Canvas.GetLeft(_movingElement);
                var currentTop = Canvas.GetTop(_movingElement);

                Canvas.SetLeft(_movingElement, currentLeft + deltaX);
                Canvas.SetTop(_movingElement, currentTop + deltaY);

                _moveStartPoint = currentPoint;
                return;
            }

            if (!_isDrawing) return;

            var point = e.GetPosition(ImageCanvas);

            if (_currentTool == "Pen" && _currentPolyline != null)
            {
                _currentPolyline.Points.Add(point);
            }
            else
            {
                if (_previewElement != null)
                {
                    ImageCanvas.Children.Remove(_previewElement);
                }

                _previewElement = CreateShape(_startPoint, point);
                if (_previewElement != null)
                {
                    ImageCanvas.Children.Add(_previewElement);
                }
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_movingElement != null)
            {
                _movingElement = null;
                ImageCanvas.ReleaseMouseCapture();
                return;
            }

            if (!_isDrawing) return;

            _isDrawing = false;
            ImageCanvas.ReleaseMouseCapture();

            var endPoint = e.GetPosition(ImageCanvas);

            if (_currentTool == "Pen" && _currentPolyline != null)
            {
                if (_currentPolyline.Points.Count > 1)
                {
                    var element = new DrawingElement { Element = _currentPolyline, Type = "Pen" };
                    _drawings.Add(element);
                    _undoStack.Push(element);
                    _redoStack.Clear();
                }
                else
                {
                    ImageCanvas.Children.Remove(_currentPolyline);
                }
                _currentPolyline = null;
            }
            else
            {
                if (_previewElement != null)
                {
                    ImageCanvas.Children.Remove(_previewElement);
                }

                var shape = CreateShape(_startPoint, endPoint);
                if (shape != null)
                {
                    ImageCanvas.Children.Add(shape);
                    var element = new DrawingElement { Element = shape, Type = _currentTool };
                    _drawings.Add(element);
                    _undoStack.Push(element);
                    _redoStack.Clear();
                }
            }

            _previewElement = null;
        }

        // 右键菜单：为最近画的线段添加箭头
        private void Canvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var pos = e.GetPosition(ImageCanvas);

            var menu = new ContextMenu();
            
            var item1 = new MenuItem { Header = "添加画笔箭头" };
            item1.Click += (s, args) => AddArrowToLastPenLine();
            menu.Items.Add(item1);
            
            var item2 = new MenuItem { Header = "删除选中" };
            item2.Click += (s, args) => DeleteSelectedElement(pos);
            menu.Items.Add(item2);

            menu.IsOpen = true;
        }

        // 右键箭头按钮：为最近画笔添加箭头
        private void AddArrowToLastPenLine_Click(object sender, RoutedEventArgs e)
        {
            AddArrowToLastPenLine();
        }

        private void AddArrowToLastPenLine()
        {
            var lastPen = _drawings.LastOrDefault(d => d.Type == "Pen");
            if (lastPen != null && lastPen.Element is Polyline polyline)
            {
                AddArrowToPenLine(polyline);
            }
            else
            {
                MessageBox.Show("没有可添加箭头的线段", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteSelectedElement(System.Windows.Point pos)
        {
            var hitTest = VisualTreeHelper.HitTest(ImageCanvas, pos);
            if (hitTest != null && hitTest.VisualHit is FrameworkElement element && element != ImageCanvas)
            {
                while (element.Parent is FrameworkElement parent && parent != ImageCanvas)
                {
                    element = parent;
                }

                var drawingElement = _drawings.FirstOrDefault(d => d.Element == element);
                if (drawingElement != null && drawingElement.Type != "BackgroundImage")
                {
                    ImageCanvas.Children.Remove(element);
                    _drawings.Remove(drawingElement);
                    _undoStack.Push(drawingElement);
                    _redoStack.Clear();
                }
            }
        }

        private void AddArrowToPenLine(Polyline polyline)
        {
            if (polyline.Points.Count < 2) return;

            // 使用路径最后一段的方向来确定箭头方向，避免回环路径导致误判
            // 取最后 N 个点（至少2个，最多10个或最后30%的点）
            int segmentCount = Math.Max(2, Math.Min(10, (int)(polyline.Points.Count * 0.3)));
            int startIndex = polyline.Points.Count - segmentCount;
            var start = polyline.Points[startIndex];
            var end = polyline.Points[polyline.Points.Count - 1];

            var canvas = new Canvas();
            var size = GetSelectedStrokeSize();

            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            var arrowLen = Math.Max(size * 4, 12);
            var arrowAngle = Math.PI / 6;

            var a1 = new Line
            {
                X1 = end.X, Y1 = end.Y,
                X2 = end.X - arrowLen * Math.Cos(angle - arrowAngle),
                Y2 = end.Y - arrowLen * Math.Sin(angle - arrowAngle),
                Stroke = _currentColor,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round
            };
            var a2 = new Line
            {
                X1 = end.X, Y1 = end.Y,
                X2 = end.X - arrowLen * Math.Cos(angle + arrowAngle),
                Y2 = end.Y - arrowLen * Math.Sin(angle + arrowAngle),
                Stroke = _currentColor,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round
            };

            canvas.Children.Add(a1);
            canvas.Children.Add(a2);

            ImageCanvas.Children.Add(canvas);

            var element = new DrawingElement { Element = canvas, Type = "PenArrow" };
            _drawings.Add(element);
            _undoStack.Push(element);
            _redoStack.Clear();
        }

        // ===== 缩放功能 =====

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom + ZoomStep);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom - ZoomStep);
        }

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            CanvasScale.ScaleX = _currentZoom;
            CanvasScale.ScaleY = _currentZoom;
            ZoomText.Text = $"{(int)(_currentZoom * 100)}%";
        }

        private void CanvasScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                if (e.Delta > 0)
                    SetZoom(_currentZoom + ZoomStep);
                else
                    SetZoom(_currentZoom - ZoomStep);
            }
        }

        // ===== 裁剪功能 =====

        private void Crop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _isCropSelecting = true;
            _cropStart = e.GetPosition(CropOverlay);
            CropOverlay.CaptureMouse();

            if (_cropRect != null)
            {
                CropOverlay.Children.Remove(_cropRect);
            }

            _cropRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x78, 0xD8)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x20, 0x3B, 0x78, 0xD8))
            };
            Canvas.SetLeft(_cropRect, _cropStart.X);
            Canvas.SetTop(_cropRect, _cropStart.Y);
            CropOverlay.Children.Add(_cropRect);
        }

        private void Crop_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isCropSelecting || _cropRect == null) return;

            var currentPoint = e.GetPosition(CropOverlay);
            var x = Math.Min(_cropStart.X, currentPoint.X);
            var y = Math.Min(_cropStart.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _cropStart.X);
            var height = Math.Abs(currentPoint.Y - _cropStart.Y);

            Canvas.SetLeft(_cropRect, x);
            Canvas.SetTop(_cropRect, y);
            _cropRect.Width = Math.Max(1, width);
            _cropRect.Height = Math.Max(1, height);
        }

        private void Crop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isCropSelecting) return;

            _isCropSelecting = false;
            CropOverlay.ReleaseMouseCapture();

            if (_cropRect != null && _cropRect.Width > 10 && _cropRect.Height > 10)
            {
                ApplyCropBtn.Visibility = Visibility.Visible;
                CancelCropBtn.Visibility = Visibility.Visible;
            }
        }

        private void ApplyCrop_Click(object sender, RoutedEventArgs e)
        {
            if (_cropRect == null) return;

            var renderBitmap = new RenderTargetBitmap(
                (int)ImageCanvas.Width,
                (int)ImageCanvas.Height,
                96, 96,
                PixelFormats.Pbgra32);
            renderBitmap.Render(ImageCanvas);

            var cropX = (int)Canvas.GetLeft(_cropRect);
            var cropY = (int)Canvas.GetTop(_cropRect);
            var cropWidth = (int)_cropRect.Width;
            var cropHeight = (int)_cropRect.Height;

            var croppedBitmap = new CroppedBitmap(renderBitmap,
                new Int32Rect(cropX, cropY, cropWidth, cropHeight));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            _originalBitmap?.Dispose();
            _originalBitmap = new Bitmap(stream);
            _bitmapSource = CaptureService.ToBitmapSource(_originalBitmap);

            ImageCanvas.Children.Clear();
            _drawings.Clear();
            _undoStack.Clear();
            _redoStack.Clear();

            InitializeCanvas();
            AddImageToCanvas();
            CancelCropMode();

            ToolPen.IsChecked = true;
        }

        private void CancelCrop_Click(object sender, RoutedEventArgs e)
        {
            CancelCropMode();
            ToolPen.IsChecked = true;
        }

        // ===== 绘图辅助 =====

        private FrameworkElement? CreateShape(System.Windows.Point start, System.Windows.Point end)
        {
            var size = GetSelectedStrokeSize();

            return _currentTool switch
            {
                "Arrow" => CreateArrow(start, end, _currentColor, size),
                "Rectangle" => CreateRectangle(start, end, _currentColor, size),
                _ => null
            };
        }

        private FrameworkElement CreateArrow(System.Windows.Point start, System.Windows.Point end, Brush color, double size)
        {
            var canvas = new Canvas();

            var line = new Line
            {
                X1 = start.X, Y1 = start.Y,
                X2 = end.X, Y2 = end.Y,
                Stroke = color,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(line);

            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            var arrowLen = Math.Max(size * 4, 12);
            var arrowAngle = Math.PI / 6;

            var a1 = new Line
            {
                X1 = end.X, Y1 = end.Y,
                X2 = end.X - arrowLen * Math.Cos(angle - arrowAngle),
                Y2 = end.Y - arrowLen * Math.Sin(angle - arrowAngle),
                Stroke = color,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round
            };
            var a2 = new Line
            {
                X1 = end.X, Y1 = end.Y,
                X2 = end.X - arrowLen * Math.Cos(angle + arrowAngle),
                Y2 = end.Y - arrowLen * Math.Sin(angle + arrowAngle),
                Stroke = color,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round
            };

            canvas.Children.Add(a1);
            canvas.Children.Add(a2);

            return canvas;
        }

        private FrameworkElement CreateRectangle(System.Windows.Point start, System.Windows.Point end, Brush color, double size)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(1, Math.Abs(end.X - start.X)),
                Height = Math.Max(1, Math.Abs(end.Y - start.Y)),
                Stroke = color,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            Canvas.SetLeft(rect, Math.Min(start.X, end.X));
            Canvas.SetTop(rect, Math.Min(start.Y, end.Y));
            return rect;
        }

        private void StartTextInput(System.Windows.Point position)
        {
            var inputWindow = new TextInputWindow(position, _currentColor, GetSelectedStrokeSize() * 4);
            inputWindow.TextConfirmed += (s, text) =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    var tb = new TextBlock
                    {
                        Text = text,
                        Foreground = _currentColor,
                        FontSize = GetSelectedStrokeSize() * 4,
                        FontWeight = FontWeights.Medium
                    };
                    Canvas.SetLeft(tb, position.X);
                    Canvas.SetTop(tb, position.Y);
                    ImageCanvas.Children.Add(tb);

                    var element = new DrawingElement { Element = tb, Type = "Text" };
                    _drawings.Add(element);
                    _undoStack.Push(element);
                    _redoStack.Clear();
                }
            };
            inputWindow.Show();
        }

        private double GetSelectedStrokeSize()
        {
            if (StrokeSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is string size)
            {
                return double.Parse(size);
            }
            return 4;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var element = _undoStack.Pop();
                ImageCanvas.Children.Remove(element.Element);
                _redoStack.Push(element);
                _drawings.Remove(element);
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count > 0)
            {
                var element = _redoStack.Pop();
                ImageCanvas.Children.Add(element.Element);
                _undoStack.Push(element);
                _drawings.Add(element);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var description = DescriptionBox.Text.Trim();

            var renderBitmap = new RenderTargetBitmap(
                (int)ImageCanvas.Width,
                (int)ImageCanvas.Height,
                96, 96,
                PixelFormats.Pbgra32);

            renderBitmap.Render(ImageCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            using var bitmap = new Bitmap(stream);
            CaptureService.SaveWithDescription(new Bitmap(bitmap), description);

            MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _originalBitmap?.Dispose();
        }
    }

    public class DrawingElement
    {
        public FrameworkElement Element { get; set; } = null!;
        public string Type { get; set; } = string.Empty;
    }
}
