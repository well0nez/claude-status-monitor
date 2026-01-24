using System;
using System.IO;
using System.Text.Json;
using ClaudeStatusMonitor.Models;

namespace ClaudeStatusMonitor.Services
{
    public static class SecureStorageService
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    var emptyConfig = new AppConfig();
                    SaveConfig(emptyConfig);
                    return emptyConfig;
                }

                var configJson = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(configJson) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(ConfigPath, json);
        }

        public static int GetRefreshInterval()
        {
            var config = LoadConfig();
            return config.RefreshIntervalMinutes <= 0 ? 2 : config.RefreshIntervalMinutes;
        }

        public static void SetRefreshInterval(int minutes)
        {
            var safeMinutes = minutes <= 0 ? 2 : minutes;
            var config = LoadConfig();
            config.RefreshIntervalMinutes = safeMinutes;
            SaveConfig(config);
        }
    }
}
