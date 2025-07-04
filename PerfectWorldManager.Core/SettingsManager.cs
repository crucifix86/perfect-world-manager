// PerfectWorldManager.Core\SettingsManager.cs
using Newtonsoft.Json;
using System;
using System.IO;

namespace PerfectWorldManager.Core
{
    public class SettingsManager
    {
        private static readonly string AppName = "PerfectWorldManager";
        private static readonly string SettingsFileName = "settings.json";

        public static string GetSettingsFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, AppName);
            return Path.Combine(appFolderPath, SettingsFileName);
        }

        public static void SaveSettings(Settings settings)
        {
            try
            {
                string filePath = GetSettingsFilePath();
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName != null && !Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                // Basic error logging to console, consider a more robust logging mechanism
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static Settings LoadSettings()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var settings = JsonConvert.DeserializeObject<Settings>(json);
                    return settings ?? new Settings(); // Return new if deserialization results in null
                }
            }
            catch (Exception ex)
            {
                // Basic error logging to console
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new Settings(); // Return default settings if file doesn't exist or an error occurs
        }
    }
}