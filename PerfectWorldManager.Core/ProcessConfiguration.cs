// PerfectWorldManager.Core\ProcessConfiguration.cs
using System.ComponentModel;

namespace PerfectWorldManager.Core
{
    // Inherit from ObservableObject to support two-way binding updates in DataGrid
    public class ProcessConfiguration : ObservableObject
    {
        private ProcessType _type;
        public ProcessType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        private bool _isEnabled = true;
        [DefaultValue(true)]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private string _executableDir = string.Empty;
        public string ExecutableDir
        {
            get => _executableDir;
            set => SetProperty(ref _executableDir, value);
        }

        private string _executableName = string.Empty;
        public string ExecutableName
        {
            get => _executableName;
            set => SetProperty(ref _executableName, value);
        }

        private string _startArguments = string.Empty;
        public string StartArguments
        {
            get => _startArguments;
            set => SetProperty(ref _startArguments, value);
        }

        private string _statusCheckPattern = string.Empty;
        public string StatusCheckPattern
        {
            get => _statusCheckPattern;
            set => SetProperty(ref _statusCheckPattern, value);
        }

        private string _mapId = string.Empty; // Added for GameServer type process
        public string MapId
        {
            get => _mapId;
            set => SetProperty(ref _mapId, value);
        }


        // Parameterless constructor for deserialization
        public ProcessConfiguration() { }

        public ProcessConfiguration(ProcessType type, string displayName, bool isEnabled,
                                    string executableDir, string executableName,
                                    string startArguments, string statusCheckPattern, string mapId = "")
        {
            _type = type;
            _displayName = displayName;
            _isEnabled = isEnabled;
            _executableDir = executableDir;
            _executableName = executableName;
            _startArguments = startArguments;
            _statusCheckPattern = statusCheckPattern;
            _mapId = mapId; // Initialize MapId
        }
    }
}