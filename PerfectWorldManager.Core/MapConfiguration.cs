// PerfectWorldManager.Core\MapConfiguration.cs
namespace PerfectWorldManager.Core
{
    public class MapConfiguration
    {
        public bool IsEnabledForAutoStart { get; set; }
        public string MapId { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;

        public MapConfiguration() { }

        public MapConfiguration(bool isEnabled, string mapId, string mapName)
        {
            IsEnabledForAutoStart = isEnabled;
            MapId = mapId;
            MapName = mapName;
        }

        public override string ToString()
        {
            return $"{(IsEnabledForAutoStart ? "yes" : "no")},{MapId},{MapName}";
        }
    }
}