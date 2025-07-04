// PerfectWorldManager.Gui\MapDisplayInfo.cs
using PerfectWorldManager.Core; // For ObservableObject and MapConfiguration

namespace PerfectWorldManager.Gui // Ensure this namespace is correct
{
    public class MapDisplayInfo : ObservableObject
    {
        private MapConfiguration _config;
        public MapConfiguration Config
        {
            get => _config;
            set => SetProperty(ref _config, value); // Keep public if you might swap out the underlying config
        }

        public bool IsEnabledForAutoStart
        {
            get => Config.IsEnabledForAutoStart;
            set
            {
                if (Config.IsEnabledForAutoStart != value)
                {
                    Config.IsEnabledForAutoStart = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MapId
        {
            get => Config.MapId;
            set
            {
                if (Config.MapId != value)
                {
                    Config.MapId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMainWorldServer)); // Re-evaluate IsMainWorldServer if MapId changes
                }
            }
        }
        public string MapName
        {
            get => Config.MapName;
            set
            {
                if (Config.MapName != value)
                {
                    Config.MapName = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isCurrentlyRunning;
        public bool IsCurrentlyRunning
        {
            get => _isCurrentlyRunning;
            set => SetProperty(ref _isCurrentlyRunning, value);
        }

        // Is this the special 'gs01' main world server map?
        public bool IsMainWorldServer => MapId.Equals("gs01", System.StringComparison.OrdinalIgnoreCase);


        public MapDisplayInfo(MapConfiguration config)
        {
            _config = config;
        }
    }
}