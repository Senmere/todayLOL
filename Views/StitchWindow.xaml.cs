using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TodayLOL.Data;
using MessageBox = System.Windows.MessageBox;

namespace TodayLOL.Views
{
    public partial class StitchWindow : Window
    {
        private List<StitchItem> _items = new();

        public StitchWindow()
        {
            InitializeComponent();
            LoadRecords();
        }

        private void LoadRecords()
        {
            _items.Clear();

            using var db = new RecordDbContext();
            var records = db.Records.OrderByDescending(r => r.CreateTime).ToList();

            foreach (var record in records)
            {
                if (File.Exists(record.FilePath))
                {
                    _items.Add(new StitchItem
                    {
                        Id = record.Id,
                        FilePath = record.FilePath,
                        Description = string.IsNullOrEmpty(record.Description) ? "(无描述)" : record.Description,
                        DateText = record.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                        Thumbnail = LoadThumbnail(record.FilePath)
                    });
                }
            }

            ImageList.ItemsSource = _items;
        }

        private BitmapImage LoadThumbnail(string path)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path);
                img.DecodePixelWidth = 96;
                img.EndInit();
                return img;
            }
            catch
            {
                return new BitmapImage();
            }
        }

        private void Direction_Changed(object sender, RoutedEventArgs e)
        {
            // 切换方向时清除预览
            PreviewImage.Source = null;
            PreviewEmpty.Visibility = Visibility.Visible;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadRecords();
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var selected = ImageList.SelectedItems.Cast<StitchItem>().ToList();
            if (selected.Count < 2)
            {
                MessageBox.Show("请至少选择2张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var isVertical = VerticalRadio.IsChecked == true;
            var stitched = StitchImages(selected.Select(s => s.FilePath).ToList(), isVertical);
            if (stitched != null)
            {
                PreviewImage.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    stitched.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                PreviewEmpty.Visibility = Visibility.Collapsed;

                // 保存到 Tag 以便保存时使用
                PreviewImage.Tag = stitched;
            }
        }

        private Bitmap? StitchImages(List<string> paths, bool isVertical)
        {
            var bitmaps = new List<Bitmap>();
            try
            {
                foreach (var path in paths)
                {
                    bitmaps.Add(new Bitmap(path));
                }

                int totalWidth, totalHeight;

                if (isVertical)
                {
                    // 纵向：取最大宽度，高度累加
                    totalWidth = bitmaps.Max(b => b.Width);
                    totalHeight = bitmaps.Sum(b => b.Height);
                }
                else
                {
                    // 横向：取最大高度，宽度累加
                    totalWidth = bitmaps.Sum(b => b.Width);
                    totalHeight = bitmaps.Max(b => b.Height);
                }

                var result = new Bitmap(totalWidth, totalHeight);
                using var g = Graphics.FromImage(result);
                g.Clear(Color.White);

                int offset = 0;
                foreach (var bmp in bitmaps)
                {
                    if (isVertical)
                    {
                        // 居中放置
                        int x = (totalWidth - bmp.Width) / 2;
                        g.DrawImage(bmp, x, offset);
                        offset += bmp.Height;
                    }
                    else
                    {
                        int y = (totalHeight - bmp.Height) / 2;
                        g.DrawImage(bmp, offset, y);
                        offset += bmp.Width;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拼接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally
            {
                // 不释放 bitmaps，因为可能还在使用
                // result 会独立保存数据
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewImage.Tag is not Bitmap stitched)
            {
                // 如果还没预览，先执行预览
                Preview_Click(sender, e);
                if (PreviewImage.Tag is not Bitmap stitched2)
                    return;
                stitched = stitched2;
            }

            var settings = Models.Settings.Instance;
            var now = DateTime.Now;
            var fileName = $"难绷_拼接_{now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(settings.SavePath, fileName);

            try
            {
                stitched.Save(filePath, ImageFormat.Png);

                using var db = new RecordDbContext();
                db.Records.Add(new Models.Record
                {
                    FilePath = filePath,
                    Description = "拼接截图",
                    CreateTime = now,
                    WatermarkPosition = settings.WatermarkPosition
                });
                db.SaveChanges();

                MessageBox.Show("拼接保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class StitchItem
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public BitmapImage Thumbnail { get; set; } = new BitmapImage();
    }
}
