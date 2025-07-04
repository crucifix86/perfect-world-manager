using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PerfectWorldManager.Core
{
    public static class PresetManager
    {
        private static readonly string PresetsFolderName = "presets";
        private static readonly string PresetFileExtension = ".json";

        public static string GetPresetsDirectoryPath()
        {
            // Get the directory where the executable is located
            string? exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDirectory))
            {
                // Fallback to current directory
                exeDirectory = Directory.GetCurrentDirectory();
            }
            
            // Go up directories until we find the project root (where presets folder should be)
            DirectoryInfo? currentDir = new DirectoryInfo(exeDirectory);
            while (currentDir != null)
            {
                // Check if presets folder exists at this level
                string presetsPath = Path.Combine(currentDir.FullName, PresetsFolderName);
                if (Directory.Exists(presetsPath))
                {
                    return presetsPath;
                }
                
                // Check if this looks like the project root (has .csproj files)
                if (currentDir.GetFiles("*.csproj").Length > 0)
                {
                    return Path.Combine(currentDir.FullName, PresetsFolderName);
                }
                
                currentDir = currentDir.Parent;
            }
            
            // Final fallback - use exe directory
            return Path.Combine(exeDirectory, PresetsFolderName);
        }

        public static void EnsurePresetsDirectoryExists()
        {
            string presetsPath = GetPresetsDirectoryPath();
            if (!Directory.Exists(presetsPath))
            {
                Directory.CreateDirectory(presetsPath);
            }
        }

        public static void SavePreset(ProcessConfigurationPreset preset)
        {
            try
            {
                EnsurePresetsDirectoryExists();
                string fileName = SanitizeFileName(preset.Name) + PresetFileExtension;
                string filePath = Path.Combine(GetPresetsDirectoryPath(), fileName);
                
                string json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving preset '{preset.Name}': {ex.Message}", ex);
            }
        }

        public static ProcessConfigurationPreset? LoadPreset(string presetName)
        {
            try
            {
                string fileName = SanitizeFileName(presetName) + PresetFileExtension;
                string filePath = Path.Combine(GetPresetsDirectoryPath(), fileName);
                
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<ProcessConfigurationPreset>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading preset '{presetName}': {ex.Message}");
            }
            return null;
        }

        public static List<ProcessConfigurationPreset> LoadAllPresets()
        {
            var presets = new List<ProcessConfigurationPreset>();
            
            try
            {
                EnsurePresetsDirectoryExists();
                string presetsPath = GetPresetsDirectoryPath();
                
                // First, create default presets if they don't exist
                CreateDefaultPresetsIfMissing();
                
                // Load all preset files
                var presetFiles = Directory.GetFiles(presetsPath, $"*{PresetFileExtension}");
                
                foreach (var file in presetFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var preset = JsonConvert.DeserializeObject<ProcessConfigurationPreset>(json);
                        if (preset != null)
                        {
                            presets.Add(preset);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading preset file '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading presets: {ex.Message}");
            }
            
            // Sort presets: read-only first (like 15x), then alphabetically
            return presets.OrderByDescending(p => p.IsReadOnly).ThenBy(p => p.Name).ToList();
        }

        public static void DeletePreset(string presetName)
        {
            try
            {
                string fileName = SanitizeFileName(presetName) + PresetFileExtension;
                string filePath = Path.Combine(GetPresetsDirectoryPath(), fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting preset '{presetName}': {ex.Message}", ex);
            }
        }

        public static bool PresetExists(string presetName)
        {
            string fileName = SanitizeFileName(presetName) + PresetFileExtension;
            string filePath = Path.Combine(GetPresetsDirectoryPath(), fileName);
            return File.Exists(filePath);
        }

        private static void CreateDefaultPresetsIfMissing()
        {
            // Create 15x preset if it doesn't exist
            string defaultPresetName = "15x";
            if (!PresetExists(defaultPresetName))
            {
                var defaultPreset = new ProcessConfigurationPreset
                {
                    Name = defaultPresetName,
                    Description = "Default 15x server configuration",
                    IsReadOnly = true,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now,
                    Configurations = GetDefault15xConfigurations()
                };
                
                SavePreset(defaultPreset);
            }
        }

        private static List<ProcessConfiguration> GetDefault15xConfigurations()
        {
            return new List<ProcessConfiguration> 
            {
                new ProcessConfiguration(ProcessType.LogService, "Log Service", true, "logservice", "./logservice", "logservice.conf", "./logservice logservice.conf"),
                new ProcessConfiguration(ProcessType.UniqueNamed, "UniqueName Daemon", true, "uniquenamed", "./uniquenamed", "gamesys.conf", "./uniquenamed gamesys.conf"),
                new ProcessConfiguration(ProcessType.AuthDaemon, "Auth Daemon", true, "authd", "./authd", "", "./authd"),
                new ProcessConfiguration(ProcessType.GameDbd, "GameDB Daemon", true, "gamedbd", "./gamedbd", "gamesys.conf", "./gamedbd gamesys.conf"),
                new ProcessConfiguration(ProcessType.GameAntiCheatDaemon, "Game AC Daemon", true, "gacd", "./gacd", "gamesys.conf", "./gacd gamesys.conf"),
                new ProcessConfiguration(ProcessType.GameFactionDaemon, "Faction Daemon", true, "gfactiond", "./gfactiond", "gamesys.conf", "./gfactiond gamesys.conf"),
                new ProcessConfiguration(ProcessType.GameDeliveryDaemon, "Delivery Daemon", true, "gdeliveryd", "./gdeliveryd", "gamesys.conf", "./gdeliveryd gamesys.conf"),
                new ProcessConfiguration(ProcessType.GameLinkDaemon, "Gateway Link", true, "glinkd", "./glinkd", "gamesys.conf", "./glinkd gamesys.conf", "glinkd"),
                new ProcessConfiguration(ProcessType.GameServer, "Game Server (gs01)", true, "gamed", "./gs", "gs01 gs.conf gmserver.conf gsalias.conf", "./gs gs01 gs.conf", "gs01"),
                new ProcessConfiguration(ProcessType.PwAdmin, "PWAdmin Panel", true, "" , "./startup.sh", "", "pwadmin"),
                new ProcessConfiguration(ProcessType.AntiCrash, "Anti-Crash", true, "" , "./anti_crash", "", "./anti_crash")
            };
        }

        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim();
        }
    }
}