using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TodayLOL.Data;
using TodayLOL.Models;
using TodayLOL.Services;

namespace TodayLOL
{
    public partial class App : System.Windows.Application
    {
        public static string DataFolder { get; private set; } = string.Empty;
        public static string SettingsPath { get; private set; } = string.Empty;
        public static string ImagesFolder { get; private set; } = string.Empty;
        public static string DbPath { get; private set; } = string.Empty;
        public static string LogPath { get; private set; } = string.Empty;

        static App()
        {
            InitPaths();
            AppDomain.CurrentDomain.UnhandledException += Static_UnhandledException;
            Log("=== 静态构造函数 ===");
        }

        private static void InitPaths()
        {
            // 使用程序目录下的 data 文件夹，避免中文路径和权限问题
            var exeDir = AppContext.BaseDirectory;
            DataFolder = Path.Combine(exeDir, "data");
            Directory.CreateDirectory(DataFolder);

            SettingsPath = Path.Combine(DataFolder, "settings.json");
            ImagesFolder = Path.Combine(DataFolder, "images");
            Directory.CreateDirectory(ImagesFolder);
            DbPath = Path.Combine(DataFolder, "records.db");
            LogPath = Path.Combine(DataFolder, "error.log");

            File.WriteAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] === InitPaths 开始 ===\r\n");
            Log($"InitPaths完成: DataFolder={DataFolder}");
        }

        private static void Static_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log($"非UI线程异常: {e.ExceptionObject}");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            Log("=== OnStartup 开始 ===");

            try
            {
                base.OnStartup(e);
                Log("base.OnStartup 完成");

                InitDatabase();
                Log("数据库初始化完成");

                Settings.Instance.Load();
                Log("设置加载完成");

                IconHelper.Init();
                Log("图标初始化完成");

                var mainWindow = new MainWindow();
                mainWindow.Show();
                Log("主窗口显示完成");

                Log("=== 启动完成 ===");
            }
            catch (Exception ex)
            {
                Log($"启动异常: {ex}");
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}\n\n详细日志: {LogPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log($"UI线程异常: {e.Exception}");
            e.Handled = true;
            System.Windows.MessageBox.Show($"发生错误: {e.Exception.Message}\n\n详细日志: {LogPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void InitDatabase()
        {
            int retryCount = 3;
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    Log($"数据库初始化尝试 {i + 1}/{retryCount}...");
                    using var db = new RecordDbContext();
                    db.Database.EnsureCreated();
                    
                    // 测试连接
                    var count = db.Records.Count();
                    Log($"数据库初始化成功，现有记录: {count}");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"数据库初始化失败: {ex.Message}");
                    
                    if (i < retryCount - 1)
                    {
                        Log("尝试修复数据库...");
                        try
                        {
                            // 备份并重新创建数据库
                            if (File.Exists(DbPath))
                            {
                                var backupPath = DbPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                                File.Copy(DbPath, backupPath, true);
                                Log($"已备份数据库到: {backupPath}");
                                File.Delete(DbPath);
                            }
                            // 删除关联文件
                            var walPath = DbPath + "-wal";
                            var shmPath = DbPath + "-shm";
                            if (File.Exists(walPath)) File.Delete(walPath);
                            if (File.Exists(shmPath)) File.Delete(shmPath);
                        }
                        catch (Exception backupEx)
                        {
                            Log($"备份数据库失败: {backupEx.Message}");
                        }
                    }
                }
            }
            Log("数据库初始化最终失败，将使用内存模式或跳过");
        }

        public static void Log(string message)
        {
            try
            {
                var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
                File.AppendAllText(LogPath, logMsg);
                Debug.WriteLine(logMsg);
            }
            catch { }
        }
    }
}
