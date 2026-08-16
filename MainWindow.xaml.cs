using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using TodayLOL.Services;
using TodayLOL.Views;

namespace TodayLOL
{
    public partial class MainWindow : Window
    {
        private RecordListView? _recordListView;
        private SettingsView? _settingsView;
        private NotifyIcon? _trayIcon;

        public string WindowTitle => Models.Settings.Instance.WindowTitle;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = WindowTitle;
            _trayIcon.Visible = true;
            _trayIcon.Icon = IconHelper.GetTrayIcon();

            var menu = new ContextMenuStrip();
            menu.Font = new System.Drawing.Font("Microsoft YaHei", 9f, System.Drawing.FontStyle.Regular);
            menu.Items.Add("显示", null, (s, e) => ShowMainWindow());
            menu.Items.Add("截图", null, (s, e) => StartCapture());
            menu.Items.Add("最近", null, (s, e) => ShowRecent());
            menu.Items.Add("设置", null, (s, e) => ShowSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => ExitApp());

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowMainWindow();
                }
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NavigateToRecords();
        }

        private void NavigateToRecords()
        {
            _recordListView ??= new RecordListView();
            MainFrame.Navigate(_recordListView);
            UpdateNavButtonState("Records");
        }

        private void NavigateToSettings()
        {
            _settingsView ??= new SettingsView();
            MainFrame.Navigate(_settingsView);
            UpdateNavButtonState("Settings");
        }

        private void UpdateNavButtonState(string activeTag)
        {
            BtnRecords.ClearValue(BackgroundProperty);
            BtnRecords.ClearValue(BorderBrushProperty);
            BtnSettings.ClearValue(BackgroundProperty);
            BtnSettings.ClearValue(BorderBrushProperty);

            var btn = activeTag == "Records" ? BtnRecords : BtnSettings;
            btn.Background = (System.Windows.Media.Brush)FindResource("HoverBrush");
            btn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                WindowState = WindowState.Normal;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void ShowMainWindow()
        {
            Show();
            Activate();
        }

        private void StartCapture()
        {
            Services.CaptureService.StartCapture(bitmap =>
            {
                if (bitmap != null && Models.Settings.Instance.AutoEditAfterCapture)
                {
                    var editor = new EditorWindow(bitmap);
                    editor.Closed += (s, e) =>
                    {
                        if (_recordListView != null)
                        {
                            _recordListView.Refresh();
                        }
                    };
                    editor.Show();
                }
            });
        }

        private void ShowRecent()
        {
            NavigateToRecords();
            ShowMainWindow();
        }

        private void ShowSettings()
        {
            NavigateToSettings();
            ShowMainWindow();
        }

        private void ExitApp()
        {
            _trayIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void NavRecords_Click(object sender, RoutedEventArgs e)
        {
            NavigateToRecords();
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSettings();
        }

        private void ToolbarCapture_Click(object sender, RoutedEventArgs e)
        {
            StartCapture();
        }

        private void ToolbarStitch_Click(object sender, RoutedEventArgs e)
        {
            var stitchWindow = new StitchWindow();
            stitchWindow.Show();
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _recordListView != null)
            {
                _recordListView.SearchFromOutside(SearchBox.Text);
            }
        }
    }
}
