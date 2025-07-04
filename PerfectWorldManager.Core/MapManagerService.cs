// PerfectWorldManager.Core\MapManagerService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfectWorldManager.Grpc; // Required for the gRPC request/response types

namespace PerfectWorldManager.Core
{
    public class MapManagerService
    {
        private readonly DaemonGrpcService _daemonService;
        private readonly Settings _settings; // Retained for local map file path and other client-side settings if any
        private readonly string _localMapsFilePath;

        private const string DefaultMapsFileContent = @"yes,gs01,World
yes,is61,Beginner World (1.5.3)
no,is62,Origination (1.5.1)
no,is01,City of Abominations
no,is02,Secret Passage
no,is03,na
no,is04,na
no,is05,Firecrag Grotto
no,is06,Den of Rabid Wolves
no,is07,Cave of the Vicious
no,is08,Hall of Deception
no,is09,Gate of Delirium
no,is10,Secret Frostcover
no,is11,Valley of Disaster
no,is12,Forest Ruins
no,is13,Cave of Sadistic Glee
no,is14,Wraithgate
no,is15,Hallucinatory Trench
no,is16,Eden
no,is17,Brimstone Pit
no,is18,Temple of the Dragon
no,is19,Nightscream Island
no,is20,Snake Isle
no,is21,Lothranis
no,is22,Momaganon
no,is23,Seat of Torment
no,is24,Abaddon
no,is25,Warsong City
no,is26,Palace of Nirvana
no,is27,Lunar Glade
no,is28,Valley of Reciprocity
no,is29,Frostcover City
no,is31,Twilight Temple
no,is32,Cube of Fate
no,is33,Chrono City
no,is34,Perfect Chapel
no,is35,Guild Base
no,is37,Morai
no,is38,Phoenix Valley
no,is39,Endless Universe
no,is40,Blighted Chamer
no,is41,Endless Universe
no,is42,Nation War Wargod Gulch
no,is43,Five Emperors
no,is44,Nation War (Flag)
no,is45,Nation War (Crystal)
no,is46,Nation War (Bridge)
no,is47,Sunset Valley
no,is48,Shutter Palace
no,is49,Dragon Hidden Den
no,is50,Realm of Reflection
no,is63,Primal World
no,is66,Flowsilver Palace
no,is67,Undercurrent Hall
no,is68,Primal World (Story Mode)
no,is69,LightSail Cave
no,is70,Cube of Fate (2)
no,is71,Dragon Counqest Battlefield
no,is72,Heavenfall Temple (base)
no,is73,Heavenfall Temple (is73)
no,is74,Heavenfall Temple (is74)
no,is75,Heavenfall Temple (is75)
no,is76,Uncharted Paradise
no,is77,Thursday Tournament
no,is80,Homestead
no,is81,Homestead
no,is82,Homestead
no,is83,Homestead
no,bg01,Territory War T-3 PvP
no,bg02,Territory War T-3 PvE
no,bg03,Territory War T-2 PvP
no,bg04,Territory War T-2 PvE
no,bg05,Territory War T-1 PvP
no,bg06,Territory War T-1 PvE
no,arena01,Etherblade Arena
no,arena02,Lost Arena
no,arena03,Plume Arena
no,arena04,Archosaur Arena
no,rand03,Quicksand Maze (rand03)
no,rand04,Quicksand Maze (rand04)"; //

        public MapManagerService(DaemonGrpcService daemonService, Settings settings)
        {
            _daemonService = daemonService ?? throw new ArgumentNullException(nameof(daemonService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _localMapsFilePath = GetLocalMapsFilePathInternal();
        }

        private string GetLocalMapsFilePathInternal() //
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); //
            string appFolderPath = Path.Combine(appDataPath, "PerfectWorldManager"); //
            Directory.CreateDirectory(appFolderPath); // Ensure it exists
            return Path.Combine(appFolderPath, "user_maps.csv"); //
        }

        public async Task EnsureLocalMapsFileExistsAsync() //
        {
            if (!File.Exists(_localMapsFilePath)) //
            {
                System.Diagnostics.Debug.WriteLine($"Local maps file not found at {_localMapsFilePath}, creating default..."); //
                try
                {
                    string? directoryName = Path.GetDirectoryName(_localMapsFilePath); //
                    if (directoryName != null && !Directory.Exists(directoryName)) //
                    {
                        Directory.CreateDirectory(directoryName); //
                    }
                    await File.WriteAllTextAsync(_localMapsFilePath, DefaultMapsFileContent); //
                    System.Diagnostics.Debug.WriteLine($"Default local maps file created at {_localMapsFilePath}"); //
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating default local maps file: {ex.Message}"); //
                    throw new IOException($"Failed to create the local maps file at '{_localMapsFilePath}'. Please check permissions or disk space. Error: {ex.Message}", ex); //
                }
            }
        }

        public async Task<List<MapConfiguration>> LoadMapConfigurationsAsync() //
        {
            await EnsureLocalMapsFileExistsAsync();
            var mapConfigs = new List<MapConfiguration>(); //
            try
            {
                string[] lines = await File.ReadAllLinesAsync(_localMapsFilePath); //
                foreach (var line in lines) //
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";")) continue; //
                    var parts = line.Split(new[] { ',' }, 3); //
                    if (parts.Length == 3) //
                    {
                        mapConfigs.Add(new MapConfiguration( //
                            parts[0].Trim().Equals("yes", StringComparison.OrdinalIgnoreCase), //
                            parts[1].Trim(), //
                            parts[2].Trim() //
                        ));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping malformed line in local maps file: {line}"); //
                    }
                }
            }
            catch (Exception ex) //
            {
                System.Diagnostics.Debug.WriteLine($"Error loading map configurations from {_localMapsFilePath}: {ex.Message}"); //
                throw new IOException($"Failed to load map configurations from '{_localMapsFilePath}'. Error: {ex.Message}", ex); //
            }
            return mapConfigs; //
        }

        public async Task SaveMapConfigurationsAsync(List<MapConfiguration> mapConfigs) //
        {
            var sb = new StringBuilder(); //
            foreach (var config in mapConfigs) //
            {
                sb.AppendLine(config.ToString()); //
            }

            try
            {
                string? directoryName = Path.GetDirectoryName(_localMapsFilePath); //
                if (directoryName != null && !Directory.Exists(directoryName)) //
                {
                    Directory.CreateDirectory(directoryName); //
                }
                await File.WriteAllTextAsync(_localMapsFilePath, sb.ToString()); //
                System.Diagnostics.Debug.WriteLine($"Map configurations saved to local file: {_localMapsFilePath}"); //
            }
            catch (Exception ex) //
            {
                System.Diagnostics.Debug.WriteLine($"Error saving map configurations to {_localMapsFilePath}: {ex.Message}"); //
                throw new IOException($"Failed to save map configurations to '{_localMapsFilePath}'. Error: {ex.Message}", ex);
            }
        }

        public async Task StartMapAsync(string mapId, bool isMainWorldServerWithMapList = false, IEnumerable<string>? additionalMapIds = null)
        {
            if (!_daemonService.IsConnected)
            {
                throw new InvalidOperationException("Not connected to the daemon service. Cannot start map.");
            }

            var request = new StartMapRequest
            {
                MapId = mapId,
                IsMainWorldServerWithMapList = isMainWorldServerWithMapList
            };
            if (additionalMapIds != null)
            {
                request.AdditionalMapIds.AddRange(additionalMapIds);
            }

            ProcessResponse response = await _daemonService.StartMapAsync(request);

            if (!response.Success)
            {
                System.Diagnostics.Debug.WriteLine($"Daemon failed to start map {mapId}: {response.Message}");
                // Consider how you want to propagate this error. Throwing an exception is one way.
                throw new Exception($"Daemon operation to start map '{mapId}' failed: {response.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"Daemon command to start map {mapId} sent. Response: {response.Message}");
        }

        public async Task StopMapAsync(string mapId)
        {
            if (!_daemonService.IsConnected)
            {
                throw new InvalidOperationException("Not connected to the daemon service. Cannot stop map.");
            }
            var request = new StopMapRequest { MapId = mapId };
            ProcessResponse response = await _daemonService.StopMapAsync(request);

            if (!response.Success)
            {
                System.Diagnostics.Debug.WriteLine($"Daemon failed to stop map {mapId}: {response.Message}");
                throw new Exception($"Daemon operation to stop map '{mapId}' failed: {response.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"Daemon command to stop map {mapId} sent. Response: {response.Message}");
        }

        public async Task<bool> IsMapRunningAsync(string mapId)
        {
            if (!_daemonService.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine($"Cannot check map status for '{mapId}': Not connected to daemon service.");
                // Depending on UI needs, you might return false or throw an exception.
                // Returning false might be friendlier for status updates if connection is temporarily lost.
                return false;
            }
            var request = new MapStatusRequest { MapId = mapId };
            MapStatusResponse response = await _daemonService.GetMapStatusAsync(request);

            System.Diagnostics.Debug.WriteLine($"Map '{mapId}' running status from daemon: {response.IsRunning}, Details: {response.Details}");
            return response.IsRunning;
        }
    }
}