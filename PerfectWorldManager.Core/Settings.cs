// PerfectWorldManager.Core/Settings.cs
using System;
using System.Collections.Generic;
using System.ComponentModel; // For INotifyPropertyChanged if ObservableObject uses it directly
using System.IO; // Required for Path.Combine
// Assuming ObservableObject.cs is in this project or referenced

namespace PerfectWorldManager.Core
{
    public class Settings : ObservableObject
    {
        #region Daemon Settings
        private string _daemonServiceUrl = "http://localhost:50051"; // Default URL for the gRPC daemon
        public string DaemonServiceUrl
        {
            get => _daemonServiceUrl;
            set => SetProperty(ref _daemonServiceUrl, value);
        }

        private string _apiKey = ""; // Added for API Key
        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }
        #endregion

        #region Path Settings (Client-side perspective or for parameters to Daemon)
        private string _serverDir = "/home"; // Example default, client's reference
        public string ServerDir { get => _serverDir; set => SetProperty(ref _serverDir, value); }

        private string _mapsFilePath = "/home/maps"; // Example default, client's reference for local map file. Daemon has its own.
        public string MapsFilePath { get => _mapsFilePath; set => SetProperty(ref _mapsFilePath, value); }

        private string _logsDir = "/home/glogs"; // Example client-side default for log viewing reference (daemon has its own LogsDir)
        public string LogsDir { get => _logsDir; set => SetProperty(ref _logsDir, value); }

        private string _pwAdminDir = "/home/pwadmin"; // Example default, client's reference for PwAdmin path context
        public string PwAdminDir { get => _pwAdminDir; set => SetProperty(ref _pwAdminDir, value); }
        #endregion

        #region Character Editor Settings
        private string _itemTxtPath = "C:\\PerfectWorld\\element\\data\\item.txt"; // Example default, user needs to configure
        public string ItemTxtPath
        {
            get => _itemTxtPath;
            set => SetProperty(ref _itemTxtPath, value);
        }

        private string _itemIconsPath = "C:\\PerfectWorld\\element\\surfaces\\iconlist_new"; // Example default, user needs to configure for item icons
        public string ItemIconsPath
        {
            get => _itemIconsPath;
            set => SetProperty(ref _itemIconsPath, value);
        }
        #endregion

        #region Localization Settings
        private string _selectedLanguage = "en-US"; // Default language
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }
        #endregion

        #region Other Settings (Retained from your existing file)
        private string _cpwDir = "/var/www/html/CPW";
        public string CpwDir { get => _cpwDir; set => SetProperty(ref _cpwDir, value); }

        private string _pwAdminUrl = "http://127.0.0.1:8080/pwadmin/AA";
        public string PwAdminUrl { get => _pwAdminUrl; set => SetProperty(ref _pwAdminUrl, value); }

        private string _pwAdminCmdFile = "cmd.jsp";
        public string PwAdminCmdFile { get => _pwAdminCmdFile; set => SetProperty(ref _pwAdminCmdFile, value); }

        private string _backupStorageDir = "/media/storage";
        public string BackupStorageDir { get => _backupStorageDir; set => SetProperty(ref _backupStorageDir, value); }
        public string FullBackupDirectoryPath => System.IO.Path.Combine(BackupStorageDir, "backup");

        private string _mySqlUser = "admin";
        public string MySqlUser { get => _mySqlUser; set => SetProperty(ref _mySqlUser, value); }

        private string _mySqlPassword = "admin";
        public string MySqlPassword { get => _mySqlPassword; set => SetProperty(ref _mySqlPassword, value); }

        private string _mySqlDatabase = "pw";
        public string MySqlDatabase { get => _mySqlDatabase; set => SetProperty(ref _mySqlDatabase, value); }

        private string _mySqlHost = "127.0.0.1";
        public string MySqlHost { get => _mySqlHost; set => SetProperty(ref _mySqlHost, value); }

        private int _mySqlPort = 3306;
        public int MySqlPort { get => _mySqlPort; set => SetProperty(ref _mySqlPort, value); }

        private int _glinkdInstances = 7;
        public int GlinkdInstances { get => _glinkdInstances; set => SetProperty(ref _glinkdInstances, value); }

        private List<ProcessConfiguration> _processConfigurations = new List<ProcessConfiguration>();
        public List<ProcessConfiguration> ProcessConfigurations { get => _processConfigurations; set => SetProperty(ref _processConfigurations, value); }
        
        private List<ProcessConfigurationPreset> _processConfigPresets = new List<ProcessConfigurationPreset>();
        public List<ProcessConfigurationPreset> ProcessConfigPresets { get => _processConfigPresets; set => SetProperty(ref _processConfigPresets, value); }
        
        private string _activePresetName = "15x";
        public string ActivePresetName { get => _activePresetName; set => SetProperty(ref _activePresetName, value); }
        #endregion

        public Settings()
        {
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BackupStorageDir))
                {
                    OnPropertyChanged(nameof(FullBackupDirectoryPath));
                }
            };
        }

        // HashCode methods for service re-initialization checks
        public int GetHashCodeDaemon() // For Daemon service
        {
            // ApiKey included in hash for daemon connection re-initialization
            return HashCode.Combine(DaemonServiceUrl, ApiKey);
        }

        public int GetHashCodePaths() // Paths used by MapManagerService or other local logic
        {
            // Added ItemTxtPath and ItemIconsPath for completeness, though they aren't directly used by MapManagerService
            // These are primarily for the GUI client's use.
            return HashCode.Combine(ServerDir, MapsFilePath, LogsDir, PwAdminDir, PwAdminUrl, ItemTxtPath, ItemIconsPath);
        }

        public int GetHashCodeDb()
        {
            return HashCode.Combine(MySqlHost, MySqlPort, MySqlUser, MySqlPassword, MySqlDatabase);
        }
    }
}