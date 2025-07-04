// PerfectWorldManager.Core\ServerProcessInfo.cs
namespace PerfectWorldManager.Core
{
    public enum ProcessType
    {
        LogService,
        UniqueNamed,
        AuthDaemon,
        GameDbd,
        GameAntiCheatDaemon, // gacd
        GameFactionDaemon, // gfactiond
        GameDeliveryDaemon, // gdeliveryd
        GameLinkDaemon, // glinkd
        GameServer, // gs (main game world instances like gs01)
        PwAdmin,
        AntiCrash
    }

    public enum ProcessStatus
    {
        Unknown,
        Stopped,
        Running,
        Starting,
        Stopping,
        Error
    }

    public class ServerProcessInfo : ObservableObject
    {
        public ProcessType Type { get; }
        public string DisplayName { get; }
        public string ExecutableDir { get; } // Store the original from ProcessConfiguration
        public string ExecutableName { get; } // Store the original
        public string StartArguments { get; } // Store the original
        public string StatusCheckPattern { get; } // Store the original
        public string MapId { get; } // Added MapId, store the original

        private ProcessStatus _status = ProcessStatus.Unknown;
        public ProcessStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _statusDetails = string.Empty;
        public string StatusDetails
        {
            get => _statusDetails;
            set => SetProperty(ref _statusDetails, value);
        }

        // Updated constructor to accept MapId (7 arguments)
        public ServerProcessInfo(ProcessType type, string displayName,
                                 string executableDir, string executableName,
                                 string startArguments, string statusCheckPattern,
                                 string mapId = "") // mapId is optional, defaults to empty
        {
            Type = type;
            DisplayName = displayName;
            ExecutableDir = executableDir;
            ExecutableName = executableName;
            StartArguments = startArguments;
            StatusCheckPattern = statusCheckPattern;
            MapId = mapId;
        }
    }
}