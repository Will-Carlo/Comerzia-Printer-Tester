using System.Text.Json;
using ComerziaPrinterTester.Models;

namespace ComerziaPrinterTester.Utils
{
    public static class ConfigStorage
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "appsettings.json"
        );

        public static AppConnectionSettings LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var settings = JsonSerializer.Deserialize<AppConnectionSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch
            {
                // Fallback to default
            }

            return new AppConnectionSettings();
        }

        public static void SaveSettings(AppConnectionSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore save errors on read-only environments
            }
        }
    }
}
