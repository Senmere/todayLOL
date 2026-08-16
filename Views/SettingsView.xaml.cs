using System.IO;
using System.Windows;
using System.Windows.Controls;
using TodayLOL.Models;

namespace TodayLOL.Views
{
    public partial class SettingsView : Page
    {
        public SettingsView()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = Settings.Instance;

            WindowTitleBox.Text = settings.WindowTitle;
            SavePathBox.Text = settings.SavePath;
            AutoStartCheckBox.IsChecked = settings.AutoStart;
            AutoEditCheckBox.IsChecked = settings.AutoEditAfterCapture;

            var positionIndex = settings.WatermarkPosition switch
            {
                "BottomRight" => 0,
                "BottomLeft" => 1,
                "TopRight" => 2,
                "TopLeft" => 3,
                _ => 0
            };
            WatermarkPositionCombo.SelectedIndex = positionIndex;
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.InitialDirectory = Settings.Instance.SavePath;

            if (dialog.ShowDialog() == true)
            {
                SavePathBox.Text = dialog.FolderName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = Settings.Instance;

            settings.WindowTitle = WindowTitleBox.Text.Trim();
            settings.SavePath = SavePathBox.Text;

            if (!Directory.Exists(settings.SavePath))
            {
                System.Windows.MessageBox.Show("保存路径不存在!", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            settings.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            settings.AutoEditAfterCapture = AutoEditCheckBox.IsChecked ?? true;

            if (WatermarkPositionCombo.SelectedItem is ComboBoxItem item)
            {
                settings.WatermarkPosition = item.Tag?.ToString() ?? "BottomRight";
            }

            settings.Save();

            // 设置开机自启
            SetAutoStart(settings.AutoStart);

            System.Windows.MessageBox.Show("设置已保存!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetAutoStart(bool autoStart)
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (regKey != null)
            {
                if (autoStart)
                {
                    regKey.SetValue("TodayLOL", exePath);
                }
                else
                {
                    regKey.DeleteValue("TodayLOL", false);
                }
                regKey.Close();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack ?? false)
            {
                NavigationService.GoBack();
            }
        }
    }
}