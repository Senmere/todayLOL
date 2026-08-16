namespace TodayLOL.Models
{
    using System.IO;
    public class Record
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public string WatermarkPosition { get; set; } = "BottomRight";
    }

    public class Settings
    {
        public string WindowTitle { get; set; } = "今日难绷";
        public string SavePath { get; set; } = string.Empty;
        public bool AutoStart { get; set; }
        public bool AutoEditAfterCapture { get; set; } = true;
        public string WatermarkPosition { get; set; } = "BottomRight";

        private static Settings? _instance;
        public static Settings Instance => _instance ??= new Settings();

        public void Load()
        {
            if (File.Exists(App.SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(App.SettingsPath);
                    var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(json);
                    if (loaded != null)
                    {
                        _instance = loaded;
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(SavePath))
            {
                SavePath = App.ImagesFolder;
            }
        }

        public void Save()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(App.SettingsPath, json);
        }
    }
}