using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TodayLOL.Data;
using TodayLOL.Models;
using TodayLOL.Services;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace TodayLOL.Views
{
    public partial class RecordListView : Page
    {
        private ObservableCollection<RecordViewModel> _records = new();
        private RecordViewModel? _selectedRecord;
        private string _searchText = string.Empty;
        private bool _isInitialized;
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;

        // 框选相关
        private bool _isSelecting;
        private System.Windows.Point _selectStart;
        private List<Border> _selectedBorders = new();
        private const int ThumbnailWidth = 120;
        private const int ThumbnailHeight = 72;
        private const int ThumbnailMargin = 6;

        public RecordListView()
        {
            InitializeComponent();
            _isInitialized = true;
            LoadRecords();
        }

        public void Refresh()
        {
            LoadRecords();
        }

        public void SearchFromOutside(string text)
        {
            _searchText = text;
            LoadRecords();
        }

        private void LoadRecords()
        {
            if (!_isInitialized) return;

            _records.Clear();
            _selectedBorders.Clear();
            ThumbnailCanvas.Children.Clear();

            using var db = new RecordDbContext();
            var query = db.Records.AsQueryable();

            if (!string.IsNullOrEmpty(_searchText))
            {
                query = query.Where(r => r.Description.Contains(_searchText));
            }

            if (_filterStartDate.HasValue)
            {
                query = query.Where(r => r.CreateTime >= _filterStartDate.Value.Date);
            }

            if (_filterEndDate.HasValue)
            {
                query = query.Where(r => r.CreateTime <= _filterEndDate.Value.Date.AddDays(1));
            }

            if (SortCombo != null)
            {
                var tag = (SortCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                query = tag switch
                {
                    "Asc" => query.OrderBy(r => r.CreateTime),
                    "Description" => query.OrderBy(r => r.Description),
                    _ => query.OrderByDescending(r => r.CreateTime)
                };
            }
            else
            {
                query = query.OrderByDescending(r => r.CreateTime);
            }

            int row = 0, col = 0;
            double canvasWidth = 0;

            foreach (var record in query)
            {
                var vm = new RecordViewModel(record);
                _records.Add(vm);

                var border = new Border
                {
                    Width = ThumbnailWidth,
                    Height = ThumbnailHeight,
                    Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA)),
                    BorderBrush = (SolidColorBrush)FindResource("BorderLightBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2),
                    Cursor = Cursors.Hand,
                    Tag = vm
                };

                var grid = new Grid();
                var image = new System.Windows.Controls.Image
                {
                    Source = vm.Thumbnail,
                    Stretch = Stretch.UniformToFill
                };
                grid.Children.Add(image);

                var dateBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(4, 1, 4, 1)
                };
                var dateText = new TextBlock
                {
                    Text = vm.DateText,
                    FontSize = 10,
                    Foreground = Brushes.White
                };
                dateBorder.Child = dateText;
                grid.Children.Add(dateBorder);

                border.Child = grid;

                border.MouseLeftButtonDown += Thumbnail_Click;
                border.MouseEnter += Thumbnail_MouseEnter;
                border.MouseLeave += Thumbnail_MouseLeave;

                Canvas.SetLeft(border, col * (ThumbnailWidth + ThumbnailMargin));
                Canvas.SetTop(border, row * (ThumbnailHeight + ThumbnailMargin));

                ThumbnailCanvas.Children.Add(border);

                col++;
                canvasWidth = Math.Max(canvasWidth, col * (ThumbnailWidth + ThumbnailMargin));
            }

            ThumbnailCanvas.Width = canvasWidth;
            ThumbnailCanvas.Height = ThumbnailHeight + ThumbnailMargin;

            if (CountText != null)
            {
                CountText.Text = $"{_records.Count} 条记录";
            }
            UpdateSelectedCount();
            UpdateEmptyState();
        }

        private void UpdateSelectedCount()
        {
            if (SelectedCountText != null)
            {
                SelectedCountText.Text = _selectedBorders.Count > 0 ? $"已选 {_selectedBorders.Count} 项" : "";
            }
        }

        private void UpdateEmptyState()
        {
            var hasRecords = _records.Count > 0;
            var hasSelection = _selectedRecord != null;

            if (EmptyPanel != null)
            {
                EmptyPanel.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
            }
            if (DetailGrid != null)
            {
                DetailGrid.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is RecordViewModel vm)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ToggleSelection(border);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (_selectedBorders.Count > 0)
                    {
                        SelectRange(border);
                    }
                    else
                    {
                        ClearSelection();
                        SelectBorder(border);
                    }
                }
                else
                {
                    ClearSelection();
                    SelectBorder(border);
                    _selectedRecord = vm;
                    ShowDetail(vm);
                }
            }
        }

        private void Thumbnail_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = (SolidColorBrush)FindResource("HoverBrush");
            }
        }

        private void Thumbnail_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA));
            }
        }

        private void SelectBorder(Border border)
        {
            if (!_selectedBorders.Contains(border))
            {
                _selectedBorders.Add(border);
                border.BorderBrush = (SolidColorBrush)FindResource("PrimaryBrush");
                border.BorderThickness = new Thickness(2);
            }
            UpdateSelectedCount();
        }

        private void DeselectBorder(Border border)
        {
            if (_selectedBorders.Contains(border))
            {
                _selectedBorders.Remove(border);
                border.BorderBrush = (SolidColorBrush)FindResource("BorderLightBrush");
                border.BorderThickness = new Thickness(1);
            }
            UpdateSelectedCount();
        }

        private void ToggleSelection(Border border)
        {
            if (_selectedBorders.Contains(border))
            {
                DeselectBorder(border);
            }
            else
            {
                SelectBorder(border);
            }
        }

        private void SelectRange(Border endBorder)
        {
            var allBorders = ThumbnailCanvas.Children.OfType<Border>().ToList();
            var startIndex = allBorders.IndexOf(_selectedBorders.Last());
            var endIndex = allBorders.IndexOf(endBorder);

            int min = Math.Min(startIndex, endIndex);
            int max = Math.Max(startIndex, endIndex);

            for (int i = min; i <= max; i++)
            {
                SelectBorder(allBorders[i]);
            }
        }

        private void ClearSelection()
        {
            foreach (var border in _selectedBorders.ToArray())
            {
                DeselectBorder(border);
            }
            _selectedBorders.Clear();
        }

        // ===== 框选功能 =====

        private void ThumbnailCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(ThumbnailCanvas);

            // 检查是否点击在缩略图上
            var hitTest = VisualTreeHelper.HitTest(ThumbnailCanvas, pos);
            if (hitTest != null && hitTest.VisualHit is Border border && border.Tag is RecordViewModel)
            {
                return;
            }

            // 开始框选
            _isSelecting = true;
            _selectStart = pos;
            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, pos.X);
            Canvas.SetTop(SelectionRect, pos.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;

            // 清空之前的选择
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                ClearSelection();
            }
        }

        private void ThumbnailCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            var currentPoint = e.GetPosition(ThumbnailCanvas);
            var x = Math.Min(_selectStart.X, currentPoint.X);
            var y = Math.Min(_selectStart.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _selectStart.X);
            var height = Math.Abs(currentPoint.Y - _selectStart.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = Math.Max(1, width);
            SelectionRect.Height = Math.Max(1, height);

            // 实时高亮选中的缩略图
            var selectionRect = new Rect(x, y, width, height);
            foreach (var child in ThumbnailCanvas.Children)
            {
                if (child is Border border && border.Tag is RecordViewModel)
                {
                    var borderRect = new Rect(
                        Canvas.GetLeft(border),
                        Canvas.GetTop(border),
                        border.Width,
                        border.Height);

                    if (selectionRect.IntersectsWith(borderRect))
                    {
                        SelectBorder(border);
                    }
                    else if (Keyboard.Modifiers != ModifierKeys.Control)
                    {
                        DeselectBorder(border);
                    }
                }
            }
        }

        private void ThumbnailCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            _isSelecting = false;
            SelectionRect.Visibility = Visibility.Collapsed;
        }

        // ===== 删除选中 =====

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBorders.Count == 0)
            {
                MessageBox.Show("请先选择要删除的记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除选中的 {_selectedBorders.Count} 条记录吗？此操作无法撤销。", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var selectedIds = _selectedBorders
                    .Select(b => ((RecordViewModel)b.Tag!).Id)
                    .ToList();

                using var db = new RecordDbContext();
                foreach (var id in selectedIds)
                {
                    var record = db.Records.Find(id);
                    if (record != null)
                    {
                        db.Records.Remove(record);
                        if (File.Exists(record.FilePath))
                        {
                            try { File.Delete(record.FilePath); } catch { }
                        }
                    }
                }
                db.SaveChanges();

                _selectedRecord = null;
                LoadRecords();
            }
        }

        // ===== 原有功能 =====

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            var tag = (FilterCombo.SelectedItem as ComboBoxItem)?.Tag as string;

            switch (tag)
            {
                case "All":
                    _filterStartDate = null;
                    _filterEndDate = null;
                    break;
                case "Today":
                    _filterStartDate = DateTime.Today;
                    _filterEndDate = DateTime.Today;
                    break;
                case "Yesterday":
                    _filterStartDate = DateTime.Today.AddDays(-1);
                    _filterEndDate = DateTime.Today.AddDays(-1);
                    break;
                case "Week":
                    var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    _filterStartDate = weekStart;
                    _filterEndDate = DateTime.Today;
                    break;
                case "Month":
                    var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    _filterStartDate = monthStart;
                    _filterEndDate = DateTime.Today;
                    break;
                case "Custom":
                    break;
            }

            LoadRecords();
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) LoadRecords();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _searchText = string.Empty;
            _filterStartDate = null;
            _filterEndDate = null;
            if (FilterCombo != null) FilterCombo.SelectedIndex = 0;
            if (SortCombo != null) SortCombo.SelectedIndex = 0;
            LoadRecords();
        }

        private void ShowDetail(RecordViewModel vm)
        {
            if (DetailGrid != null) DetailGrid.Visibility = Visibility.Visible;
            if (EmptyPanel != null) EmptyPanel.Visibility = Visibility.Collapsed;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(vm.FilePath);
                image.EndInit();
                if (DetailImage != null) DetailImage.Source = image;
            }
            catch
            {
                if (DetailImage != null) DetailImage.Source = null;
            }

            if (DetailDate != null) DetailDate.Text = vm.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
            if (DetailDescription != null) DetailDescription.Text = vm.Description;
        }

        private void CopyDescription_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecord != null && !string.IsNullOrEmpty(_selectedRecord.Description))
            {
                Clipboard.SetText(_selectedRecord.Description);
                MessageBox.Show("描述已复制到剪贴板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("没有可复制的描述。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecord != null && File.Exists(_selectedRecord.FilePath))
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = Path.GetFileName(_selectedRecord.FilePath);
                saveDialog.Filter = "PNG图片|*.png";
                saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(_selectedRecord.FilePath, saveDialog.FileName, true);
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecord != null)
            {
                var result = MessageBox.Show("确定要删除这条截图记录吗？此操作无法撤销。", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    using var db = new RecordDbContext();
                    var record = db.Records.Find(_selectedRecord.Id);
                    if (record != null)
                    {
                        db.Records.Remove(record);
                        db.SaveChanges();

                        if (File.Exists(record.FilePath))
                        {
                            try { File.Delete(record.FilePath); } catch { }
                        }
                    }

                    _selectedRecord = null;
                    LoadRecords();
                }
            }
        }
    }

    public class RecordViewModel
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public BitmapImage Thumbnail { get; set; } = new BitmapImage();
        public string DateText => CreateTime.ToString("MM-dd HH:mm");

        public RecordViewModel(Record record)
        {
            Id = record.Id;
            FilePath = record.FilePath;
            Description = record.Description;
            CreateTime = record.CreateTime;
            LoadThumbnail();
        }

        private void LoadThumbnail()
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(FilePath);
                image.DecodePixelWidth = 160;
                image.EndInit();
                Thumbnail = image;
            }
            catch { }
        }
    }
}
