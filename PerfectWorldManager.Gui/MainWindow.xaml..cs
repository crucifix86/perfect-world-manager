using PerfectWorldManager.Core;
using PerfectWorldManager.Core.Utils;
// Converters are used in XAML, not typically directly in code-behind unless for dynamic resource creation
// using PerfectWorldManager.Gui.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using PerfectWorldManager.Grpc;
using PerfectWorldManager.Gui.ViewModels; // For CharacterEditorViewModel
// Assuming CharacterEditorView.xaml.cs is in namespace PerfectWorldManager.Gui
// If you placed CharacterEditorView.xaml in a subfolder like "Views", the using would be:
// using PerfectWorldManager.Gui.Views;
using PerfectWorldManager.Core.Services; // For IItemLookupService and ItemLookupService
// using PerfectWorldManager.Gui.Utils; // For CharacterXmlParser (if it's there) - Ensure this is correct if ItemLookupService depends on it internally
using PerfectWorldManager.Gui.Services;
using PerfectWorldManager.Gui.Dialogs;

// ADDED for Localization
using System.Globalization;
using System.Threading;

namespace PerfectWorldManager.Gui
{
    public partial class MainWindow : Window
    {
        public Settings AppSettings { get; set; }
        private DaemonGrpcService? _daemonService;
        private DatabaseService? _dbService;
        private MapManagerService? _mapManagerService;
        private CharacterEditorViewModel? _characterEditorViewModel; // Added for Character Editor

        public ObservableCollection<ServerProcessInfo> ServerProcesses { get; set; } = new();
        public ObservableCollection<MapDisplayInfo> DisplayableMaps { get; set; } = new();
        public ObservableCollection<ProcessConfigurationPreset> ProcessConfigPresets { get; set; } = new();

        private int _daemonService_SettingsHash;
        private int _mapManagerService_SettingsHash;
        private DaemonGrpcService? _mapManagerService_DaemonServiceInstance;
        private int _dbService_SettingsHash;

        // For accessing named TabItems, ensure they have x:Name in XAML
        private TabItem? DashboardTabItem => FindName("DashboardTab") as TabItem;
        private TabItem? AccountsTabItem => FindName("AccountsTab") as TabItem;
        private TabItem? MapManagementTabItem => FindName("MapManagementTab") as TabItem;
        private TabItem? CharacterEditorTabItem => FindName("CharacterEditorTab") as TabItem; // Added for Character Editor


        public MainWindow()
        {
            InitializeComponent();
            // MODIFIED for Localization: AppSettings now loaded by App.xaml.cs
            AppSettings = App.AppSettings;

            // Load presets from file system
            LoadPresetsFromFiles();

            // Load the active preset configurations
            var activePreset = PresetManager.LoadPreset(AppSettings.ActivePresetName);
            if (activePreset != null)
            {
                AppSettings.ProcessConfigurations = activePreset.Configurations.ToList();
            }
            else
            {
                // Fallback to 15x if active preset not found
                AppSettings.ActivePresetName = "15x";
                activePreset = PresetManager.LoadPreset("15x");
                if (activePreset != null)
                {
                    AppSettings.ProcessConfigurations = activePreset.Configurations.ToList();
                }
                else
                {
                    // Ultimate fallback - use in-memory defaults
                    AppSettings.ProcessConfigurations = GetDefaultProcessConfigurations();
                }
            }

            this.DataContext = this; // Make sure DataContext is set for bindings in XAML
            LoadSettingsToUi();
            InitializeServerProcessList();
            InitializeLanguageSelector(); // ADDED for Localization
            
            // Initialize notification system
            NotificationManager.Initialize(NotificationContainer);

            if (this.FindName("AddCubiAmountTextBox") is TextBox cubiTextBox)
            {
                cubiTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            }
            InitializeServices(true); // Force initial service setup
        }

        // ADDED for Localization
        private void InitializeLanguageSelector()
        {
            if (this.FindName("LanguageSelectorComboBox") is ComboBox langComboBox) // Ensure LanguageSelectorComboBox is the x:Name in XAML
            {
                foreach (ComboBoxItem item in langComboBox.Items)
                {
                    if (item.Tag?.ToString() == AppSettings.SelectedLanguage)
                    {
                        langComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        // ADDED for Localization
        private void LanguageSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string? languageCode = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(languageCode) && languageCode != AppSettings.SelectedLanguage)
                {
                    App.SwitchLanguage(languageCode); // AppSettings.SelectedLanguage is updated in App.SwitchLanguage
                    SettingsManager.SaveSettings(AppSettings); // Save settings after language change

                    // Optional: Inform user or refresh UI if necessary.
                    // if (this.FindName("StatusBarText") is TextBlock statusBar) 
                    // {
                    //    statusBar.Text = Application.Current.TryFindResource("Status_LanguageChangedRestart") as string ?? "Language changed. Restart may be required for full effect.";
                    // }
                }
            }
        }

        private void InitializeServerProcessList()
        {
            ServerProcesses.Clear();
            if (AppSettings.ProcessConfigurations == null) return;

            foreach (var pc in AppSettings.ProcessConfigurations.Where(p => p.IsEnabled))
            {
                ServerProcesses.Add(new ServerProcessInfo(
                    pc.Type, pc.DisplayName, pc.ExecutableDir,
                    pc.ExecutableName, pc.StartArguments, pc.StatusCheckPattern, pc.MapId));
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void LoadSettingsToUi()
        {
            // Existing settings
            if (this.FindName("DaemonUrlTextBox") is TextBox daemonUrlBox) daemonUrlBox.Text = AppSettings.DaemonServiceUrl ?? string.Empty;
            if (this.FindName("ApiKeyTextBox") is TextBox apiKeyBox) apiKeyBox.Text = AppSettings.ApiKey ?? string.Empty; // Load API Key
            if (this.FindName("ServerDirTextBox") is TextBox serverDirBox) serverDirBox.Text = AppSettings.ServerDir ?? string.Empty;
            if (this.FindName("MapsFilePathTextBox") is TextBox mapsFilePathBox) mapsFilePathBox.Text = AppSettings.MapsFilePath ?? string.Empty;
            if (this.FindName("LogsDirTextBox") is TextBox logsDirBox) logsDirBox.Text = AppSettings.LogsDir ?? string.Empty;
            if (this.FindName("PwAdminDirTextBox") is TextBox pwAdminDirBox) pwAdminDirBox.Text = AppSettings.PwAdminDir ?? string.Empty;
            if (this.FindName("PwAdminUrlTextBox") is TextBox pwAdminUrlBox) pwAdminUrlBox.Text = AppSettings.PwAdminUrl ?? string.Empty;
            if (this.FindName("BackupStorageDirTextBox") is TextBox backupStorageDirBox) backupStorageDirBox.Text = AppSettings.BackupStorageDir ?? string.Empty;
            if (this.FindName("MySqlHostTextBox") is TextBox mySqlHostBox) mySqlHostBox.Text = AppSettings.MySqlHost ?? string.Empty;
            if (this.FindName("MySqlPortTextBox") is TextBox mySqlPortBox) mySqlPortBox.Text = AppSettings.MySqlPort.ToString();
            if (this.FindName("MySqlUserTextBox") is TextBox mySqlUserBox) mySqlUserBox.Text = AppSettings.MySqlUser ?? string.Empty;
            if (this.FindName("MySqlPasswordBox") is PasswordBox mySqlPasswordBox) mySqlPasswordBox.Password = AppSettings.MySqlPassword ?? string.Empty;
            if (this.FindName("MySqlDatabaseTextBox") is TextBox mySqlDatabaseBox) mySqlDatabaseBox.Text = AppSettings.MySqlDatabase ?? string.Empty;

            // Load new settings for Character Editor (ensure TextBoxes ItemTxtPathTextBox & ItemIconsPathTextBox exist in XAML)
            if (this.FindName("ItemTxtPathTextBox") is TextBox itemTxtPathBox) itemTxtPathBox.Text = AppSettings.ItemTxtPath ?? string.Empty;
            if (this.FindName("ItemIconsPathTextBox") is TextBox itemIconsPathBox) itemIconsPathBox.Text = AppSettings.ItemIconsPath ?? string.Empty;
        }

        private void UpdateAppSettingsFromUi()
        {
            // Existing settings
            if (this.FindName("DaemonUrlTextBox") is TextBox daemonUrlBox) AppSettings.DaemonServiceUrl = daemonUrlBox.Text;
            if (this.FindName("ApiKeyTextBox") is TextBox apiKeyBox) AppSettings.ApiKey = apiKeyBox.Text; // Save API Key
            if (this.FindName("ServerDirTextBox") is TextBox serverDirBox) AppSettings.ServerDir = serverDirBox.Text;
            if (this.FindName("MapsFilePathTextBox") is TextBox mapsFilePathBox) AppSettings.MapsFilePath = mapsFilePathBox.Text;
            if (this.FindName("LogsDirTextBox") is TextBox logsDirBox) AppSettings.LogsDir = logsDirBox.Text;
            if (this.FindName("PwAdminDirTextBox") is TextBox pwAdminDirBox) AppSettings.PwAdminDir = pwAdminDirBox.Text;
            if (this.FindName("PwAdminUrlTextBox") is TextBox pwAdminUrlBox) AppSettings.PwAdminUrl = pwAdminUrlBox.Text;
            if (this.FindName("BackupStorageDirTextBox") is TextBox backupStorageDirBox) AppSettings.BackupStorageDir = backupStorageDirBox.Text;
            if (this.FindName("MySqlHostTextBox") is TextBox mySqlHostBox) AppSettings.MySqlHost = mySqlHostBox.Text;
            if (this.FindName("MySqlPortTextBox") is TextBox mySqlPortBox && int.TryParse(mySqlPortBox.Text, out int mysqlPort)) AppSettings.MySqlPort = mysqlPort; else AppSettings.MySqlPort = 3306;
            if (this.FindName("MySqlUserTextBox") is TextBox mySqlUserBox) AppSettings.MySqlUser = mySqlUserBox.Text;
            if (this.FindName("MySqlPasswordBox") is PasswordBox mySqlPasswordBox) AppSettings.MySqlPassword = mySqlPasswordBox.Password; // Note: PasswordBox.Password is not bindable directly for security. This is okay for this app's model.
            if (this.FindName("MySqlDatabaseTextBox") is TextBox mySqlDatabaseBox) AppSettings.MySqlDatabase = mySqlDatabaseBox.Text;

            // Update new settings for Character Editor
            if (this.FindName("ItemTxtPathTextBox") is TextBox itemTxtPathBox) AppSettings.ItemTxtPath = itemTxtPathBox.Text;
            if (this.FindName("ItemIconsPathTextBox") is TextBox itemIconsPathBox) AppSettings.ItemIconsPath = itemIconsPathBox.Text;
            // Language setting is updated via its own ComboBox event handler and App.SwitchLanguage
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            UpdateAppSettingsFromUi();
            SettingsManager.SaveSettings(AppSettings);
            StatusBarText.Text = "Settings saved. Reconnect or re-initialize services for changes to take full effect."; // Consider localizing
            NotificationManager.ShowSuccess("Settings Saved", "Configuration has been saved successfully");
            InitializeServerProcessList();
            InitializeServices(true); // Force re-init of services, including CharacterEditorViewModel
        }

        private void GenerateApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.FindName("ApiKeyTextBox") is TextBox apiKeyBox)
            {
                apiKeyBox.Text = Guid.NewGuid().ToString("N"); // Generates a 32-character hex string
                AppSettings.ApiKey = apiKeyBox.Text; // Update the AppSettings directly as well
                StatusBarText.Text = "New API Key generated. Save settings to persist."; // Consider localizing
            }
        }

        #region Preset Management Methods
        
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                AppSettings.ActivePresetName = selectedPreset.Name;
            }
        }

        private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                var result = MessageBox.Show(
                    $"Load preset '{selectedPreset.Name}'? This will replace your current process configurations.",
                    "Load Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Load fresh from file to ensure we have latest version
                    var presetFromFile = PresetManager.LoadPreset(selectedPreset.Name);
                    if (presetFromFile != null)
                    {
                        // Load the preset configurations
                        AppSettings.ProcessConfigurations = presetFromFile.Configurations.Select(c => new ProcessConfiguration
                        {
                            Type = c.Type,
                            DisplayName = c.DisplayName,
                            IsEnabled = c.IsEnabled,
                            ExecutableDir = c.ExecutableDir,
                            ExecutableName = c.ExecutableName,
                            StartArguments = c.StartArguments,
                            StatusCheckPattern = c.StatusCheckPattern,
                            MapId = c.MapId
                        }).ToList();

                        AppSettings.ActivePresetName = selectedPreset.Name;
                        
                        // Refresh the DataGrid
                        ProcessConfigDataGrid.ItemsSource = null;
                        ProcessConfigDataGrid.ItemsSource = AppSettings.ProcessConfigurations;
                        
                        StatusBarText.Text = $"Loaded preset '{selectedPreset.Name}'. Remember to save settings.";
                        NotificationManager.ShowSuccess("Preset Loaded", $"Configuration preset '{selectedPreset.Name}' has been loaded");
                    }
                    else
                    {
                        MessageBox.Show($"Failed to load preset '{selectedPreset.Name}' from file.", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SavePresetAsButton_Click(object sender, RoutedEventArgs e)
        {
            var nameDialog = new InputDialog(
                "Enter a name for this preset:",
                "Save Preset As",
                "My Custom Preset")
            {
                Owner = this
            };

            if (nameDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(nameDialog.ResponseText))
            {
                var nameInput = nameDialog.ResponseText;
                
                // Check if preset name already exists
                if (PresetManager.PresetExists(nameInput))
                {
                    MessageBox.Show($"A preset with the name '{nameInput}' already exists. Please choose a different name.",
                        "Preset Name Exists",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var descriptionDialog = new InputDialog(
                    "Enter a description for this preset (optional):",
                    "Preset Description",
                    "")
                {
                    Owner = this
                };
                
                string descriptionInput = "";
                if (descriptionDialog.ShowDialog() == true)
                {
                    descriptionInput = descriptionDialog.ResponseText;
                }

                var newPreset = new ProcessConfigurationPreset
                {
                    Name = nameInput,
                    Description = descriptionInput,
                    IsReadOnly = false,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now,
                    Configurations = AppSettings.ProcessConfigurations.Select(c => new ProcessConfiguration
                    {
                        Type = c.Type,
                        DisplayName = c.DisplayName,
                        IsEnabled = c.IsEnabled,
                        ExecutableDir = c.ExecutableDir,
                        ExecutableName = c.ExecutableName,
                        StartArguments = c.StartArguments,
                        StatusCheckPattern = c.StatusCheckPattern,
                        MapId = c.MapId
                    }).ToList()
                };

                // Save preset to file
                PresetManager.SavePreset(newPreset);
                AppSettings.ActivePresetName = newPreset.Name;
                
                // Reload presets from files
                LoadPresetsFromFiles();
                PresetComboBox.SelectedValue = newPreset.Name;
                
                StatusBarText.Text = $"Created new preset '{nameInput}'. Remember to save settings.";
                NotificationManager.ShowSuccess("Preset Created", $"Configuration preset '{nameInput}' has been created");
            }
        }

        private void UpdatePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                if (selectedPreset.IsReadOnly)
                {
                    MessageBox.Show("Cannot update read-only presets.", "Read-Only Preset", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Update preset '{selectedPreset.Name}' with current configurations?",
                    "Update Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Create updated preset
                    var updatedPreset = new ProcessConfigurationPreset
                    {
                        Name = selectedPreset.Name,
                        Description = selectedPreset.Description,
                        IsReadOnly = selectedPreset.IsReadOnly,
                        CreatedDate = selectedPreset.CreatedDate,
                        LastModifiedDate = DateTime.Now,
                        Configurations = AppSettings.ProcessConfigurations.Select(c => new ProcessConfiguration
                        {
                            Type = c.Type,
                            DisplayName = c.DisplayName,
                            IsEnabled = c.IsEnabled,
                            ExecutableDir = c.ExecutableDir,
                            ExecutableName = c.ExecutableName,
                            StartArguments = c.StartArguments,
                            StatusCheckPattern = c.StatusCheckPattern,
                            MapId = c.MapId
                        }).ToList()
                    };
                    
                    // Save updated preset to file
                    PresetManager.SavePreset(updatedPreset);
                    
                    // Reload presets
                    LoadPresetsFromFiles();
                    
                    StatusBarText.Text = $"Updated preset '{selectedPreset.Name}'. Remember to save settings.";
                    NotificationManager.ShowSuccess("Preset Updated", $"Configuration preset '{selectedPreset.Name}' has been updated");
                }
            }
        }

        private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                if (selectedPreset.IsReadOnly)
                {
                    MessageBox.Show("Cannot delete read-only presets.", "Read-Only Preset", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete preset '{selectedPreset.Name}'?",
                    "Delete Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Delete preset file
                    PresetManager.DeletePreset(selectedPreset.Name);
                    
                    // Switch to default preset if deleted was active
                    if (AppSettings.ActivePresetName == selectedPreset.Name)
                    {
                        AppSettings.ActivePresetName = "15x";
                        LoadPresetButton_Click(sender, e);
                    }
                    
                    // Reload presets from files
                    LoadPresetsFromFiles();
                    PresetComboBox.SelectedValue = AppSettings.ActivePresetName;
                    
                    StatusBarText.Text = $"Deleted preset '{selectedPreset.Name}'. Remember to save settings.";
                    NotificationManager.ShowWarning("Preset Deleted", $"Configuration preset '{selectedPreset.Name}' has been deleted");
                }
            }
        }
        
        #endregion


        private void InitializeServices(bool forceReinitialize = false)
        {
            int currentDaemonSettingsHash = AppSettings.GetHashCodeDaemon(); // This now includes ApiKey
            bool daemonServiceNeedsReinit = forceReinitialize || _daemonService == null || _daemonService_SettingsHash != currentDaemonSettingsHash || (_daemonService != null && !_daemonService.IsConnected);

            if (daemonServiceNeedsReinit)
            {
                _daemonService?.Dispose();
                _daemonService = new DaemonGrpcService(AppSettings); // AppSettings now includes ApiKey
                _daemonService_SettingsHash = currentDaemonSettingsHash;

                _daemonService.ConnectionAttempting += (s, ev) => Dispatcher.Invoke(() => { 
                    StatusBarText.Text = "Connecting to daemon..."; 
                    _characterEditorViewModel?.UpdateCommandStates(); 
                    UpdateConnectionStatus(false, "Connecting...");
                }); // Consider localizing
                _daemonService.ConnectionEstablished += (s, ev) => Dispatcher.Invoke(async () => {
                    StatusBarText.Text = "Connected to daemon."; // Consider localizing
                    _characterEditorViewModel?.UpdateCommandStates();
                    UpdateConnectionStatus(true);
                    NotificationManager.ShowSuccess("Connected", "Successfully connected to daemon service");
                    var selectedItem = MainTabControl.SelectedItem as TabItem;
                    if (selectedItem == DashboardTabItem || selectedItem == MapManagementTabItem)
                    {
                        await RefreshServerStatus();
                        if (selectedItem == MapManagementTabItem && _mapManagerService != null)
                        {
                            await RefreshMapsListAndStatusAsync();
                        }
                    }
                });
                _daemonService.ConnectionFailed += (s, errMsg) => Dispatcher.Invoke(() => {
                    StatusBarText.Text = $"Daemon connection failed: {errMsg}"; // Consider localizing
                    _characterEditorViewModel?.UpdateCommandStates();
                    UpdateConnectionStatus(false);
                    NotificationManager.ShowError("Connection Failed", errMsg);
                });
                _daemonService.Disconnected += (s, ev) => Dispatcher.Invoke(() => {
                    StatusBarText.Text = "Disconnected from daemon."; // Consider localizing
                    foreach (var proc in ServerProcesses) { proc.Status = ProcessStatus.Unknown; proc.StatusDetails = "Daemon Disconnected"; } // Consider localizing
                    if (DisplayableMaps != null) foreach (var map in DisplayableMaps) { map.IsCurrentlyRunning = false; }
                    _characterEditorViewModel?.UpdateCommandStates();
                    UpdateConnectionStatus(false);
                    NotificationManager.ShowWarning("Disconnected", "Connection to daemon service lost");
                });
            }

            // Initialize or update CharacterEditorViewModel
            if (daemonServiceNeedsReinit || _characterEditorViewModel == null)
            {
                if (_daemonService != null)
                {
                    _characterEditorViewModel?.Cleanup();
                    IItemLookupService itemLookupService = new ItemLookupService();
                    _characterEditorViewModel = new CharacterEditorViewModel(_daemonService, AppSettings, itemLookupService);
                    if (this.FindName("CharacterEditorViewControl") is CharacterEditorViewThemed cev)
                    {
                        cev.DataContext = _characterEditorViewModel;
                    }
                }
            }
            _characterEditorViewModel?.UpdateCommandStates();


            int currentPathSettingsHash = AppSettings.GetHashCodePaths();
            if (forceReinitialize || _mapManagerService == null || _mapManagerService_SettingsHash != currentPathSettingsHash || _mapManagerService_DaemonServiceInstance != _daemonService)
            {
                if (_daemonService != null)
                {
                    _mapManagerService = new MapManagerService(_daemonService, AppSettings);
                    _mapManagerService_SettingsHash = currentPathSettingsHash;
                    _mapManagerService_DaemonServiceInstance = _daemonService;
                }
                else _mapManagerService = null;
            }

            int currentDbSettingsHash = AppSettings.GetHashCodeDb();
            if (forceReinitialize || _dbService == null || _dbService_SettingsHash != currentDbSettingsHash)
            {
                _dbService = new DatabaseService(AppSettings);
                _dbService_SettingsHash = currentDbSettingsHash;
            }
        }

        private async void ConnectDaemonButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAppSettingsFromUi(); // Ensure ApiKey is read from UI before initializing
            InitializeServices(true);

            if (_daemonService == null)
            {
                MessageBox.Show("Daemon Service could not be initialized. Check settings.", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error); // Consider localizing
                StatusBarText.Text = "Daemon service init failed."; // Consider localizing
                return;
            }
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                MessageBox.Show("API Key is missing in settings. Please configure an API Key.", "API Key Missing", MessageBoxButton.OK, MessageBoxImage.Warning); // Consider localizing
                StatusBarText.Text = "API Key is missing."; // Consider localizing
                return;
            }


            StatusBarText.Text = "Attempting to connect to daemon..."; // Consider localizing
            var client = _daemonService.GetClient(true); // This forces a connection attempt

            await Task.Delay(200);

            if (_daemonService.IsConnected && client != null)
            {
                await RefreshServerStatus();
                if (MainTabControl.SelectedItem == MapManagementTabItem && _mapManagerService != null)
                {
                    await RefreshMapsListAndStatusAsync();
                }
                if (MainTabControl.SelectedItem == CharacterEditorTabItem && _characterEditorViewModel != null)
                {
                    _characterEditorViewModel.UpdateCommandStates();
                }
            }
            else
            {
                if (!_daemonService.IsConnected)
                {
                    // The ConnectionFailed event in InitializeServices should already show a detailed message.
                }
            }
        }

        public async void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshServerStatus();
        }

        private async Task RefreshServerStatus()
        {
            if (_daemonService == null) { InitializeServices(); }
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                StatusBarText.Text = "Cannot refresh: Not connected to daemon service."; // Consider localizing
                foreach (var proc in ServerProcesses) { proc.Status = ProcessStatus.Unknown; proc.StatusDetails = "Daemon not connected"; } // Consider localizing
                _characterEditorViewModel?.UpdateCommandStates();
                return;
            }
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                StatusBarText.Text = "Cannot refresh: API Key is missing in settings."; // Consider localizing
                _characterEditorViewModel?.UpdateCommandStates();
                return;
            }

            StatusBarText.Text = "Refreshing server status via daemon..."; // Consider localizing
            var tasks = new List<Task>();
            foreach (var processInfo in ServerProcesses)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var procConfig = AppSettings.ProcessConfigurations.FirstOrDefault(pc => pc.Type == processInfo.Type && pc.DisplayName == processInfo.DisplayName && pc.MapId == processInfo.MapId);
                    if (procConfig == null || !procConfig.IsEnabled)
                    {
                        await Dispatcher.InvokeAsync(() => { processInfo.Status = ProcessStatus.Unknown; processInfo.StatusDetails = procConfig == null ? "Config Missing" : "Disabled"; }); // Consider localizing
                        return;
                    }
                    try
                    {
                        await Dispatcher.InvokeAsync(() => processInfo.StatusDetails = "Checking..."); // Consider localizing
                        var statusRequest = new ProcessStatusRequest
                        {
                            ProcessKey = $"{procConfig.Type}_{procConfig.DisplayName}_{procConfig.MapId}".Replace(" ", "_"),
                            StatusCheckPattern = procConfig.StatusCheckPattern
                        };
                        var grpcResponse = await _daemonService.GetProcessStatusAsync(statusRequest);

                        ProcessStatus newStatus;
                        string newDetails = grpcResponse.Details ?? "N/A";

                        switch (grpcResponse.Status)
                        {
                            case Grpc.ProcessStatusResponse.Types.Status.Running: newStatus = ProcessStatus.Running; break;
                            case Grpc.ProcessStatusResponse.Types.Status.Stopped: newStatus = ProcessStatus.Stopped; break;
                            case Grpc.ProcessStatusResponse.Types.Status.Starting: newStatus = ProcessStatus.Starting; break;
                            case Grpc.ProcessStatusResponse.Types.Status.Stopping: newStatus = ProcessStatus.Stopping; break;
                            case Grpc.ProcessStatusResponse.Types.Status.Error: newStatus = ProcessStatus.Error; break;
                            default: newStatus = ProcessStatus.Unknown; break;
                        }
                        await Dispatcher.InvokeAsync(() => { processInfo.Status = newStatus; processInfo.StatusDetails = newDetails; });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() => { processInfo.Status = ProcessStatus.Error; processInfo.StatusDetails = $"Client Error: {ex.Message.Split('\n')[0]}"; }); // Consider localizing
                    }
                }));
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during parallel status refresh: {ex.Message}");
                await Dispatcher.InvokeAsync(() => StatusBarText.Text = $"Error during status refresh: {ex.Message.Split('\n')[0]}"); // Consider localizing
            }
            await Dispatcher.InvokeAsync(() => {
                StatusBarText.Text = "Server status refreshed."; // Consider localizing
                UpdateServiceCounts();
            });
            _characterEditorViewModel?.UpdateCommandStates();
        }

        private async void StartAllServicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_daemonService == null) InitializeServices();
            if (_daemonService == null || !_daemonService.IsConnected) { MessageBox.Show("Not connected to daemon service.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing. Cannot start services.", "API Key Error"); return; } // Consider localizing

            var standardServicesStartOrder = new[] { ProcessType.LogService, ProcessType.UniqueNamed, ProcessType.AuthDaemon, ProcessType.GameDbd, ProcessType.GameAntiCheatDaemon, ProcessType.GameFactionDaemon, ProcessType.GameDeliveryDaemon };
            StatusBarText.Text = "Starting all services via daemon..."; this.IsEnabled = false; var overallStopwatch = Stopwatch.StartNew(); // Consider localizing
            try
            {
                StatusBarText.Text = "Running chmod.sh via daemon..."; // Consider localizing
                var chmodRequest = new ExecuteCommandRequest { Command = "./chmod.sh", WorkingDirectory = AppSettings.ServerDir?.Replace("\\", "/") ?? string.Empty };
                var chmodResponse = await _daemonService.ExecuteCommandAsync(chmodRequest);
                if (chmodResponse.Success) { StatusBarText.Text = "chmod.sh executed via daemon."; } else { StatusBarText.Text = $"chmod.sh via daemon failed: {chmodResponse.ErrorOutput}"; Debug.WriteLine($"chmod.sh failed: {chmodResponse.ErrorOutput} {chmodResponse.Output}"); } // Consider localizing
                await Task.Delay(500);
                foreach (var processTypeToStart in standardServicesStartOrder) { var procConfig = AppSettings.ProcessConfigurations.FirstOrDefault(p => p.Type == processTypeToStart && p.IsEnabled); var procInfo = ServerProcesses.FirstOrDefault(p => p.Type == processTypeToStart); if (procConfig != null && procInfo != null) { if (procInfo.Status != ProcessStatus.Running) { StatusBarText.Text = $"Starting {procConfig.DisplayName}..."; await StartProcessFromConfig(procConfig, procInfo); await Task.Delay(GetDelayForProcess(procConfig.Type)); } } else { Debug.WriteLine($"Config/UI for {processTypeToStart} not found/enabled."); } } // Consider localizing
                var glinkdConfig = AppSettings.ProcessConfigurations.FirstOrDefault(p => p.Type == ProcessType.GameLinkDaemon && p.IsEnabled); var glinkdInfoForUI = ServerProcesses.FirstOrDefault(p => p.Type == ProcessType.GameLinkDaemon);
                if (glinkdConfig != null) { StatusBarText.Text = "Starting glinkd instances via daemon..."; for (int i = 1; i <= AppSettings.GlinkdInstances; i++) { StatusBarText.Text = $"Starting glinkd instance {i}..."; string glinkdArgs = $"{glinkdConfig.StartArguments} {i}"; var tempGlinkdConfig = new ProcessConfiguration(glinkdConfig.Type, $"{glinkdConfig.DisplayName} {i}", glinkdConfig.IsEnabled, glinkdConfig.ExecutableDir, glinkdConfig.ExecutableName, glinkdArgs, glinkdConfig.StatusCheckPattern.Contains("{i}") ? glinkdConfig.StatusCheckPattern.Replace("{i}", i.ToString()) : glinkdConfig.StatusCheckPattern, glinkdConfig.MapId); var tempGlinkdInfo = new ServerProcessInfo(tempGlinkdConfig.Type, tempGlinkdConfig.DisplayName, tempGlinkdConfig.ExecutableDir, tempGlinkdConfig.ExecutableName, tempGlinkdConfig.StartArguments, tempGlinkdConfig.StatusCheckPattern, tempGlinkdConfig.MapId); await StartProcessFromConfig(tempGlinkdConfig, tempGlinkdInfo); await Task.Delay(1000); } if (glinkdInfoForUI != null) { glinkdInfoForUI.StatusDetails = $"{AppSettings.GlinkdInstances} instances start commands sent."; glinkdInfoForUI.Status = ProcessStatus.Unknown; } } // Consider localizing
                var antiCrashConfig = AppSettings.ProcessConfigurations.FirstOrDefault(p => p.Type == ProcessType.AntiCrash && p.IsEnabled); var antiCrashInfoForUI = ServerProcesses.FirstOrDefault(p => p.Type == ProcessType.AntiCrash); if (antiCrashConfig != null && antiCrashInfoForUI != null) { if (antiCrashInfoForUI.Status != ProcessStatus.Running) { StatusBarText.Text = $"Starting {antiCrashConfig.DisplayName}..."; await StartProcessFromConfig(antiCrashConfig, antiCrashInfoForUI); await Task.Delay(1000); } } // Consider localizing
                var pwAdminConfig = AppSettings.ProcessConfigurations.FirstOrDefault(p => p.Type == ProcessType.PwAdmin && p.IsEnabled); var pwAdminInfoForUI = ServerProcesses.FirstOrDefault(p => p.Type == ProcessType.PwAdmin); if (pwAdminConfig != null && pwAdminInfoForUI != null && pwAdminInfoForUI.Status != ProcessStatus.Running) { StatusBarText.Text = $"Starting {pwAdminConfig.DisplayName}..."; await StartProcessFromConfig(pwAdminConfig, pwAdminInfoForUI); await Task.Delay(2000); } // Consider localizing
                await LoadMapsAndStartMainGameServerAsync(); overallStopwatch.Stop(); StatusBarText.Text = $"Start All completed in {overallStopwatch.Elapsed.TotalSeconds:F1}s."; MessageBox.Show("Start All Services sequence finished via daemon.", "Operation Complete"); // Consider localizing
            }
            catch (Exception ex) { overallStopwatch.Stop(); StatusBarText.Text = $"Error during Start All via daemon: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error during start all via daemon: {ex.Message}", "Error"); } // Consider localizing
            finally { this.IsEnabled = true; await RefreshServerStatus(); }
        }

        private async void StopAllServicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_daemonService == null) InitializeServices();
            if (_daemonService == null || !_daemonService.IsConnected) { MessageBox.Show("Not connected to daemon service.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing. Cannot stop services.", "API Key Error"); return; } // Consider localizing

            var shutdownOrder = new[] { ProcessType.PwAdmin, ProcessType.AntiCrash, ProcessType.LogService, ProcessType.GameLinkDaemon, ProcessType.GameAntiCheatDaemon, ProcessType.GameServer, ProcessType.GameFactionDaemon, ProcessType.GameDeliveryDaemon, ProcessType.UniqueNamed, ProcessType.GameDbd, ProcessType.AuthDaemon };
            StatusBarText.Text = "Stopping all services via daemon..."; this.IsEnabled = false; var overallStopwatch = Stopwatch.StartNew(); // Consider localizing
            try
            {
                foreach (var processTypeToStop in shutdownOrder) { var procsToStop = ServerProcesses.Where(p => p.Type == processTypeToStop).ToList(); if (!procsToStop.Any()) { Debug.WriteLine($"No UI object for {processTypeToStop} to stop."); continue; } foreach (var procInfoForUI in procsToStop) { if (procInfoForUI.Status == ProcessStatus.Running || procInfoForUI.Status == ProcessStatus.Starting || procInfoForUI.Status == ProcessStatus.Unknown) { StatusBarText.Text = $"Stopping {procInfoForUI.DisplayName}..."; await StopProcess(procInfoForUI); await Task.Delay(500); } else { StatusBarText.Text = $"{procInfoForUI.DisplayName} already stopped/error."; await Task.Delay(100); } } } // Consider localizing
                StatusBarText.Text = "Clearing server cache via daemon..."; // Consider localizing
                try
                {
                    var cacheClearRequest = new ExecuteCommandRequest { Command = "echo 3 | sudo tee /proc/sys/vm/drop_caches > /dev/null" };
                    var cacheClearResponse = await _daemonService.ExecuteCommandAsync(cacheClearRequest);
                    if (cacheClearResponse.Success) { Debug.WriteLine($"Cache clear attempted via daemon. Output: {cacheClearResponse.Output}"); StatusBarText.Text = "Server cache clear command sent to daemon."; } else { Debug.WriteLine($"Cache clear via daemon failed: {cacheClearResponse.ErrorOutput}"); StatusBarText.Text = "Error clearing cache via daemon (may require sudo on daemon)."; } // Consider localizing
                    await Task.Delay(1000);
                }
                catch (Exception ex) { StatusBarText.Text = "Error clearing cache (may require sudo on daemon)."; Debug.WriteLine($"Cache clear error: {ex.Message}"); } // Consider localizing
                overallStopwatch.Stop(); StatusBarText.Text = $"Stop All completed in {overallStopwatch.Elapsed.TotalSeconds:F1}s."; MessageBox.Show("Stop All Services sequence finished via daemon.", "Operation Complete"); // Consider localizing
            }
            catch (Exception ex) { overallStopwatch.Stop(); StatusBarText.Text = $"Error during Stop All via daemon: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error during stop all via daemon: {ex.Message}", "Error"); } // Consider localizing
            finally { this.IsEnabled = true; await RefreshServerStatus(); }
        }

        private int GetDelayForProcess(ProcessType type) { return type switch { ProcessType.LogService => 1000, ProcessType.UniqueNamed => 1500, ProcessType.AuthDaemon => 2000, ProcessType.GameDbd => 1500, ProcessType.GameAntiCheatDaemon => 1500, ProcessType.GameFactionDaemon => 1500, ProcessType.GameDeliveryDaemon => 2000, _ => 1000, }; }

        private async Task LoadMapsAndStartMainGameServerAsync(bool isExplicitGs01Start = false)
        {
            if (_daemonService == null || !_daemonService.IsConnected || _mapManagerService == null) { StatusBarText.Text = "Cannot start game server: Daemon/Map service not init/connected."; return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { StatusBarText.Text = "API Key is missing. Cannot start game server."; return; } // Consider localizing

            var gameServerConfig = AppSettings.ProcessConfigurations.FirstOrDefault(p => p.Type == ProcessType.GameServer && p.MapId == "gs01" && p.IsEnabled);
            var gameServerInfoForUI = ServerProcesses.FirstOrDefault(p => p.Type == ProcessType.GameServer && p.MapId == "gs01");
            if (gameServerConfig == null || gameServerInfoForUI == null) { Debug.WriteLine("gs01 config/UI object not found/enabled."); if (isExplicitGs01Start) MessageBox.Show("gs01 definition not found/disabled in Process Configurations.", "Error"); return; } // Consider localizing
            StatusBarText.Text = "Loading maps from local configuration..."; // Consider localizing
            await Dispatcher.InvokeAsync(() => { gameServerInfoForUI.StatusDetails = "Preparing maps..."; gameServerInfoForUI.Status = ProcessStatus.Starting; }); // Consider localizing
            List<MapConfiguration> allMapConfigs;
            try
            {
                allMapConfigs = await _mapManagerService.LoadMapConfigurationsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading local map configs for gs01: {ex.Message}");
                await Dispatcher.InvokeAsync(() => { gameServerInfoForUI.Status = ProcessStatus.Error; gameServerInfoForUI.StatusDetails = "Error loading local maps file."; StatusBarText.Text = "Error loading local map configs."; }); // Consider localizing
                MessageBox.Show($"Could not load map configurations: {ex.Message}", "Map Config Error", MessageBoxButton.OK, MessageBoxImage.Error); return; // Consider localizing
            }
            List<string> enabledSubMapIds = allMapConfigs.Where(mc => mc.IsEnabledForAutoStart && !mc.MapId.Equals("gs01", StringComparison.OrdinalIgnoreCase)).Select(mc => mc.MapId).ToList();
            try
            {
                await _mapManagerService.StartMapAsync("gs01", true, enabledSubMapIds);
                await Task.Delay(3000);
                await Dispatcher.InvokeAsync(() => gameServerInfoForUI.StatusDetails = $"Started with {enabledSubMapIds.Count} auto-start maps."); // Consider localizing
                await RefreshServerStatus();
                StatusBarText.Text = "Main Game Server (gs01) start command sent to daemon."; // Consider localizing
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting gs01: {ex.Message}");
                await Dispatcher.InvokeAsync(() => { gameServerInfoForUI.Status = ProcessStatus.Error; gameServerInfoForUI.StatusDetails = $"Error starting: {ex.Message.Split('\n')[0]}"; StatusBarText.Text = "Error starting gs01 via daemon."; }); // Consider localizing
                MessageBox.Show($"Error starting gs01 via daemon: {ex.Message}", "gs01 Start Error", MessageBoxButton.OK, MessageBoxImage.Error); // Consider localizing
            }
        }

        private async void StartSingleProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing in settings. Cannot start process.", "API Key Error"); return; } // Consider localizing
            if (sender is Button button && button.Tag is ServerProcessInfo procInfoForUI)
            {
                var procConfig = AppSettings.ProcessConfigurations.FirstOrDefault(pc => pc.Type == procInfoForUI.Type && pc.DisplayName == procInfoForUI.DisplayName && pc.MapId == procInfoForUI.MapId);
                if (procConfig != null && procConfig.IsEnabled) await StartProcessFromConfig(procConfig, procInfoForUI);
                else MessageBox.Show($"Config for {procInfoForUI.DisplayName} not found/disabled.", "Error"); // Consider localizing
            }
        }

        private async void StopSingleProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing in settings. Cannot stop process.", "API Key Error"); return; } // Consider localizing
            if (sender is Button button && button.Tag is ServerProcessInfo procInfoForUI)
            {
                await StopProcess(procInfoForUI);
            }
        }

        private async Task StartProcessFromConfig(ProcessConfiguration procConfig, ServerProcessInfo procInfoForUI)
        {
            if (_daemonService == null) { InitializeServices(); }
            if (_daemonService == null || !_daemonService.IsConnected)
            { MessageBox.Show("Not connected to daemon service.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                MessageBox.Show("API Key is missing in settings. Cannot start process.", "API Key Error"); // Consider localizing
                await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = "API Key Missing"; }); // Consider localizing
                return;
            }

            try
            {
                await Dispatcher.InvokeAsync(() => { procInfoForUI.StatusDetails = "Starting via daemon..."; procInfoForUI.Status = ProcessStatus.Starting; }); // Consider localizing
                StatusBarText.Text = $"Starting {procConfig.DisplayName} via daemon..."; // Consider localizing

                string workingDirForDaemon;
                string executableForDaemon = procConfig.ExecutableName;

                if (procConfig.Type == ProcessType.PwAdmin)
                {
                    workingDirForDaemon = string.IsNullOrEmpty(procConfig.ExecutableDir) ?
                                          (AppSettings.PwAdminDir?.Replace("\\", "/") ?? AppSettings.ServerDir?.Replace("\\", "/") ?? string.Empty) :
                                          Path.Combine(AppSettings.PwAdminDir?.Replace("\\", "/") ?? AppSettings.ServerDir?.Replace("\\", "/") ?? string.Empty, procConfig.ExecutableDir).Replace("\\", "/");
                }
                else if (string.IsNullOrEmpty(procConfig.ExecutableDir))
                {
                    workingDirForDaemon = AppSettings.ServerDir?.Replace("\\", "/") ?? string.Empty;
                }
                else
                {
                    workingDirForDaemon = Path.Combine(AppSettings.ServerDir?.Replace("\\", "/") ?? string.Empty, procConfig.ExecutableDir).Replace("\\", "/");
                }

                if (!executableForDaemon.StartsWith("/") && !executableForDaemon.StartsWith("./") && !Path.IsPathRooted(executableForDaemon))
                {
                    executableForDaemon = "./" + executableForDaemon;
                }

                string logFileNameBase = procConfig.Type.ToString().ToLower();
                if (!string.IsNullOrEmpty(procConfig.MapId)) logFileNameBase += $"_{procConfig.MapId}";
                else if (!string.IsNullOrEmpty(procConfig.DisplayName) && procConfig.DisplayName.Contains(" ")) logFileNameBase += $"_{procConfig.DisplayName.Split(' ')[0]}";
                else if (!string.IsNullOrEmpty(procConfig.DisplayName)) logFileNameBase += $"_{procConfig.DisplayName}";
                logFileNameBase = Regex.Replace(logFileNameBase, @"[^a-zA-Z0-9_.-]", "");


                var startRequest = new StartProcessRequest
                {
                    ProcessKey = $"{procConfig.Type}_{procConfig.DisplayName}_{procConfig.MapId}".Replace(" ", "_"),
                    ExecutableName = executableForDaemon,
                    Arguments = procConfig.StartArguments,
                    WorkingDirectory = workingDirForDaemon,
                    LogFileNameBase = logFileNameBase
                };

                var grpcResponse = await _daemonService.StartProcessAsync(startRequest);

                if (grpcResponse.Success)
                {
                    await Task.Delay(1000);
                    await RefreshServerStatus();
                    StatusBarText.Text = $"{procConfig.DisplayName} start command sent. {grpcResponse.Message}"; // Consider localizing
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = $"Daemon Start Error: {grpcResponse.Message}"; }); // Consider localizing
                    StatusBarText.Text = $"Error starting {procConfig.DisplayName}: {grpcResponse.Message}"; // Consider localizing
                    MessageBox.Show($"Daemon error starting {procConfig.DisplayName}: {grpcResponse.Message}", "Error"); // Consider localizing
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = $"Client Start Error: {ex.Message.Split('\n')[0]}"; }); // Consider localizing
                StatusBarText.Text = $"Error starting {procConfig.DisplayName}: {ex.Message.Split('\n')[0]}"; // Consider localizing
                MessageBox.Show($"Error starting {procConfig.DisplayName}: {ex.Message}", "Error"); // Consider localizing
            }
        }

        private async Task StopProcess(ServerProcessInfo procInfoForUI)
        {
            if (_daemonService == null) InitializeServices();
            if (_daemonService == null || !_daemonService.IsConnected)
            { MessageBox.Show("Not connected to daemon service.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                MessageBox.Show("API Key is missing in settings. Cannot stop process.", "API Key Error"); // Consider localizing
                await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = "API Key Missing"; }); // Consider localizing
                return;
            }

            var procConfig = AppSettings.ProcessConfigurations.FirstOrDefault(pc => pc.Type == procInfoForUI.Type && pc.DisplayName == procInfoForUI.DisplayName && pc.MapId == procInfoForUI.MapId);
            if (procConfig == null)
            {
                MessageBox.Show($"Config for {procInfoForUI.DisplayName} not found.", "Error"); // Consider localizing
                await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = "Config not found"; }); // Consider localizing
                return;
            }
            try
            {
                await Dispatcher.InvokeAsync(() => { procInfoForUI.StatusDetails = "Stopping via daemon..."; procInfoForUI.Status = ProcessStatus.Stopping; }); // Consider localizing
                StatusBarText.Text = $"Stopping {procConfig.DisplayName} via daemon..."; // Consider localizing

                var stopRequest = new StopProcessRequest
                {
                    ProcessKey = $"{procConfig.Type}_{procConfig.DisplayName}_{procConfig.MapId}".Replace(" ", "_"),
                    StatusCheckPattern = procConfig.StatusCheckPattern
                };

                var grpcResponse = await _daemonService.StopProcessAsync(stopRequest);
                if (grpcResponse.Success)
                {
                    await Task.Delay(500);
                    await RefreshServerStatus();
                    StatusBarText.Text = $"{procConfig.DisplayName} stop command sent. {grpcResponse.Message}"; // Consider localizing
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = $"Daemon Stop Error: {grpcResponse.Message}"; }); // Consider localizing
                    StatusBarText.Text = $"Error stopping {procConfig.DisplayName}: {grpcResponse.Message}"; // Consider localizing
                    MessageBox.Show($"Daemon error stopping {procConfig.DisplayName}: {grpcResponse.Message}", "Error"); // Consider localizing
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => { procInfoForUI.Status = ProcessStatus.Error; procInfoForUI.StatusDetails = $"Client Stop Error: {ex.Message.Split('\n')[0]}"; }); // Consider localizing
                StatusBarText.Text = $"Error stopping {procConfig.DisplayName}: {ex.Message.Split('\n')[0]}"; // Consider localizing
                MessageBox.Show($"Error stopping {procConfig.DisplayName}: {ex.Message}", "Error"); // Consider localizing
            }
        }

        private void InitializeDbService()
        {
            if (_dbService == null || AppSettings.GetHashCodeDb() != _dbService_SettingsHash)
            {
                UpdateAppSettingsFromUi();
                _dbService = new DatabaseService(AppSettings);
                _dbService_SettingsHash = AppSettings.GetHashCodeDb();
            }
        }

        private async void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {
            //InitializeDbService(); // No longer needed for this operation
            //if (_dbService == null) { if (FindName("CreateUserStatusTextBlock") is TextBlock tb) tb.Text = "DB service not init."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                if (FindName("CreateUserStatusTextBlock") is TextBlock tb) tb.Text = "Daemon service not connected."; // Consider localizing
                return;
            }
            string username = (FindName("CreateUserUsernameTextBox") as TextBox)?.Text ?? "";
            string password = (FindName("CreateUserPasswordBox") as PasswordBox)?.Password ?? "";
            string email = (FindName("CreateUserEmailTextBox") as TextBox)?.Text ?? "";
            if (FindName("CreateUserStatusTextBlock") is TextBlock statusTb) statusTb.Text = "Processing via daemon..."; // Consider localizing

            var request = new CreateAccountRequest { Username = username, Password = password, Email = email };
            AccountActionResponse response = await _daemonService.CreateAccountAsync(request);

            if (FindName("CreateUserStatusTextBlock") is TextBlock finalStatusTb) finalStatusTb.Text = response.Message;
            if (response.Success) { (FindName("CreateUserUsernameTextBox") as TextBox)?.Clear(); (FindName("CreateUserPasswordBox") as PasswordBox)?.Clear(); (FindName("CreateUserEmailTextBox") as TextBox)?.Clear(); await LoadBrowseAccountsAsync(); }
        }

        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            //InitializeDbService();
            //if (_dbService == null) { if (FindName("ChangePassStatusTextBlock") is TextBlock tb) tb.Text = "DB service not init."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                if (FindName("ChangePassStatusTextBlock") is TextBlock tb) tb.Text = "Daemon service not connected."; // Consider localizing
                return;
            }
            string username = (FindName("ChangePassUsernameTextBox") as TextBox)?.Text ?? "";
            string oldPassword = (FindName("ChangePassOldPasswordBox") as PasswordBox)?.Password ?? "";
            string newPassword = (FindName("ChangePassNewPasswordBox") as PasswordBox)?.Password ?? "";
            if (FindName("ChangePassStatusTextBlock") is TextBlock statusTb) statusTb.Text = "Processing via daemon..."; // Consider localizing

            var request = new ChangePasswordRequest { Username = username, OldPassword = oldPassword, NewPassword = newPassword };
            AccountActionResponse response = await _daemonService.ChangePasswordAsync(request);

            if (FindName("ChangePassStatusTextBlock") is TextBlock finalStatusTb) finalStatusTb.Text = response.Message;
            if (response.Success) { (FindName("ChangePassUsernameTextBox") as TextBox)?.Clear(); (FindName("ChangePassOldPasswordBox") as PasswordBox)?.Clear(); (FindName("ChangePassNewPasswordBox") as PasswordBox)?.Clear(); }
        }

        private async void AddCubiButton_Click(object sender, RoutedEventArgs e)
        {
            //InitializeDbService();
            //if (_dbService == null) { if (FindName("AddCubiStatusTextBlock") is TextBlock tb) tb.Text = "DB service not init."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                if (FindName("AddCubiStatusTextBlock") is TextBlock tb) tb.Text = "Daemon service not connected."; // Consider localizing
                return;
            }
            string identifier = (FindName("AddCubiIdentifierTextBox") as TextBox)?.Text ?? "";
            ComboBox? idTypeCombo = FindName("AddCubiIdentifierTypeComboBox") as ComboBox;
            bool isById = ((ComboBoxItem)idTypeCombo?.SelectedItem)?.Content.ToString() == "User ID"; // This will need adjustment if ComboBoxItem content is localized
            TextBox? amountTextBox = FindName("AddCubiAmountTextBox") as TextBox;
            if (!int.TryParse(amountTextBox?.Text, out int amount)) { if (FindName("AddCubiStatusTextBlock") is TextBlock tb) tb.Text = "Invalid amount."; return; } // Consider localizing
            if (FindName("AddCubiStatusTextBlock") is TextBlock statusTb) statusTb.Text = "Processing via daemon..."; // Consider localizing

            var request = new AddCubiRequest { Identifier = identifier, IsById = isById, Amount = amount };
            AccountActionResponse response = await _daemonService.AddCubiAsync(request);

            if (FindName("AddCubiStatusTextBlock") is TextBlock finalStatusTb) finalStatusTb.Text = response.Message;
            if (response.Success) { (FindName("AddCubiIdentifierTextBox") as TextBox)?.Clear(); amountTextBox?.Clear(); }
        }

        private async void SetGmStatusButton_Click(object sender, RoutedEventArgs e)
        {
            //InitializeDbService();
            //if (_dbService == null) { if (FindName("SetGmStatusTextBlock") is TextBlock tb) tb.Text = "DB service not init."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                if (FindName("SetGmStatusTextBlock") is TextBlock tb) tb.Text = "Daemon service not connected."; // Consider localizing
                return;
            }
            string identifier = (FindName("SetGmIdentifierTextBox") as TextBox)?.Text ?? "";
            ComboBox? idTypeCombo = FindName("SetGmIdentifierTypeComboBox") as ComboBox;
            bool isById = ((ComboBoxItem)idTypeCombo?.SelectedItem)?.Content.ToString() == "User ID"; // This will need adjustment
            ComboBox? actionCombo = FindName("SetGmActionComboBox") as ComboBox;
            bool grantAccess = ((ComboBoxItem)actionCombo?.SelectedItem)?.Content.ToString() == "Grant GM Access"; // This will need adjustment
            if (FindName("SetGmStatusTextBlock") is TextBlock statusTb) statusTb.Text = "Processing via daemon..."; // Consider localizing

            var request = new SetGmStatusRequest { Identifier = identifier, IsById = isById, GrantAccess = grantAccess };
            AccountActionResponse response = await _daemonService.SetGmStatusAsync(request);

            if (FindName("SetGmStatusTextBlock") is TextBlock finalStatusTb) finalStatusTb.Text = response.Message;
            if (response.Success) { await LoadBrowseAccountsAsync(); }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            //InitializeDbService();
            //if (_dbService == null) { if (FindName("DeleteUserStatusTextBlock") is TextBlock tb) tb.Text = "DB service not init."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                if (FindName("DeleteUserStatusTextBlock") is TextBlock tb) tb.Text = "Daemon service not connected."; // Consider localizing
                return;
            }
            string identifier = (FindName("DeleteUserIdentifierTextBox") as TextBox)?.Text ?? "";
            ComboBox? idTypeCombo = FindName("DeleteUserIdentifierTypeComboBox") as ComboBox;
            bool isById = ((ComboBoxItem)idTypeCombo?.SelectedItem)?.Content.ToString() == "User ID"; // This will need adjustment
            var confirmResult = MessageBox.Show($"Delete account: {identifier}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning); // Consider localizing
            if (confirmResult != MessageBoxResult.Yes) { if (FindName("DeleteUserStatusTextBlock") is TextBlock tb) tb.Text = "Deletion cancelled."; return; } // Consider localizing
            if (FindName("DeleteUserStatusTextBlock") is TextBlock statusTb) statusTb.Text = "Processing via daemon..."; // Consider localizing

            var request = new DeleteUserRequest { Identifier = identifier, IsById = isById };
            AccountActionResponse response = await _daemonService.DeleteUserAsync(request);

            if (FindName("DeleteUserStatusTextBlock") is TextBlock finalStatusTb) finalStatusTb.Text = response.Message;
            if (response.Success) { (FindName("DeleteUserIdentifierTextBox") as TextBox)?.Clear(); await LoadBrowseAccountsAsync(); }
        }

        private async void BrowseAccountsRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadBrowseAccountsAsync();
        }

        private async Task LoadBrowseAccountsAsync()
        {
            //InitializeDbService();
            //if (_dbService == null) { StatusBarText.Text = "DB service not init for Browse."; return; } // Consider localizing
            if (_daemonService == null || !_daemonService.IsConnected)
            {
                StatusBarText.Text = "Daemon service not connected. Cannot load accounts."; // Consider localizing
                if (FindName("BrowseAccountsListView") is ListView lv) lv.ItemsSource = null;
                return;
            }
            StatusBarText.Text = "Loading accounts via daemon..."; // Consider localizing
            try
            {
                var request = new GetAllUsersRequest();
                GetAllUsersResponse response = await _daemonService.GetAllUsersAsync(request);

                if (response.Success)
                {
                    var accounts = response.Users.Select(u => new UserAccountInfo // Core.UserAccountInfo
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        CreateTime = DateTime.TryParse(u.CreateTime, out var dt) ? dt : DateTime.MinValue,
                        IsGm = u.IsGm
                    }).ToList();

                    if (FindName("BrowseAccountsListView") is ListView lv) lv.ItemsSource = accounts;
                    StatusBarText.Text = $"Loaded {accounts.Count} accounts via daemon. {response.Message}"; // Consider localizing
                }
                else
                {
                    StatusBarText.Text = $"Error loading accounts via daemon: {response.Message}"; // Consider localizing
                    if (FindName("BrowseAccountsListView") is ListView lv) lv.ItemsSource = null;
                    MessageBox.Show($"Failed to load accounts via daemon: {response.Message}", "Error"); // Consider localizing
                }
            }
            catch (Exception ex) { StatusBarText.Text = $"Client error loading accounts: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Failed to load accounts: {ex.Message}", "Error"); } // Consider localizing
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource is TabControl tabControl && tabControl.Name == "MainTabControl") // Changed e.Source to e.OriginalSource for reliability
            {
                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    InitializeServices(); // Ensure services are ready

                    if (selectedTab == CharacterEditorTabItem)
                    {
                        if (this.FindName("CharacterEditorViewControl") is CharacterEditorViewThemed cev && cev.DataContext != _characterEditorViewModel)
                        {
                            cev.DataContext = _characterEditorViewModel;
                        }
                        _characterEditorViewModel?.UpdateCommandStates();
                        
                        // Load first page of all characters if not already loaded
                        if (_characterEditorViewModel?.AllCharactersList.Count == 0 && _characterEditorViewModel?.CurrentPage == 1)
                        {
                            _characterEditorViewModel.RefreshAllCharactersCommand.Execute(null);
                        }
                    }
                    else if (selectedTab == AccountsTabItem && (_dbService != null))
                    {
                        await LoadBrowseAccountsAsync();
                    }
                    else if (selectedTab == MapManagementTabItem && (_mapManagerService != null))
                    {
                        await RefreshMapsListAndStatusAsync();
                    }
                    else if (selectedTab == DashboardTabItem && _daemonService != null && _daemonService.IsConnected)
                    {
                        if (!string.IsNullOrWhiteSpace(AppSettings.ApiKey))
                        {
                            await RefreshServerStatus();
                        }
                        else
                        {
                            StatusBarText.Text = "Dashboard: API Key is missing in settings. Cannot refresh status."; // Consider localizing
                        }
                    }
                }
            }
        }

        private async Task RefreshMapsListAndStatusAsync()
        {
            if (_mapManagerService == null && _daemonService != null) { InitializeServices(); }
            if (_mapManagerService == null) { StatusBarText.Text = "Map service not initialized."; DisplayableMaps.Clear(); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey) && _daemonService != null && _daemonService.IsConnected)
            {
                StatusBarText.Text = "Map Management: API Key is missing. Statuses cannot be fetched."; // Consider localizing
                try
                {
                    List<MapConfiguration> mapConfigs = await _mapManagerService.LoadMapConfigurationsAsync();
                    DisplayableMaps.Clear();
                    foreach (var config in mapConfigs)
                    {
                        var mdi = new MapDisplayInfo(config);
                        mdi.IsCurrentlyRunning = false;
                        DisplayableMaps.Add(mdi);
                    }
                    StatusBarText.Text = $"Loaded {DisplayableMaps.Count} map definitions. API Key needed for live statuses."; // Consider localizing
                }
                catch (IOException ioEx) { StatusBarText.Text = $"Error loading local map file: {ioEx.Message.Split('\n')[0]}"; MessageBox.Show($"Error loading map list from local file: {ioEx.Message}", "Map File Error", MessageBoxButton.OK, MessageBoxImage.Error); } // Consider localizing
                catch (Exception ex) { StatusBarText.Text = $"Error refreshing maps: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error loading map list: {ex.Message}", "Map Error", MessageBoxButton.OK, MessageBoxImage.Error); } // Consider localizing
                return;
            }

            StatusBarText.Text = "Refreshing map list from local file..."; // Consider localizing
            try
            {
                List<MapConfiguration> mapConfigs = await _mapManagerService.LoadMapConfigurationsAsync();
                DisplayableMaps.Clear();
                foreach (var config in mapConfigs) { DisplayableMaps.Add(new MapDisplayInfo(config)); }

                StatusBarText.Text = $"Loaded {DisplayableMaps.Count} map definitions. Updating statuses (requires daemon connection)..."; // Consider localizing
                if (_daemonService != null && _daemonService.IsConnected) { await UpdateAllMapRunningStatusesAsync(); }
                else { StatusBarText.Text = $"Loaded {DisplayableMaps.Count} maps. Connect to daemon to see running statuses."; foreach (var mapDispInfo in DisplayableMaps) { mapDispInfo.IsCurrentlyRunning = false; } } // Consider localizing
            }
            catch (IOException ioEx) { StatusBarText.Text = $"Error loading local map file: {ioEx.Message.Split('\n')[0]}"; MessageBox.Show($"Error loading map list from local file: {ioEx.Message}", "Map File Error", MessageBoxButton.OK, MessageBoxImage.Error); } // Consider localizing
            catch (Exception ex) { StatusBarText.Text = $"Error refreshing maps: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error loading map list: {ex.Message}", "Map Error", MessageBoxButton.OK, MessageBoxImage.Error); } // Consider localizing
        }

        private async Task UpdateSingleMapRunningStatusAsync(MapDisplayInfo mapDisplay)
        {
            if (_mapManagerService == null || _daemonService == null || !_daemonService.IsConnected || string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                mapDisplay.IsCurrentlyRunning = false; return;
            }
            mapDisplay.IsCurrentlyRunning = await _mapManagerService.IsMapRunningAsync(mapDisplay.MapId);
        }

        private async Task UpdateAllMapRunningStatusesAsync()
        {
            if (_mapManagerService == null || _daemonService == null || !_daemonService.IsConnected || string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                StatusBarText.Text = "Cannot update map statuses: Daemon not connected, API Key missing, or map service not ready."; // Consider localizing
                foreach (var mapDispInfo in DisplayableMaps) { mapDispInfo.IsCurrentlyRunning = false; }
                return;
            }
            StatusBarText.Text = "Updating map running statuses via daemon..."; // Consider localizing
            var tasks = DisplayableMaps.Select(UpdateSingleMapRunningStatusAsync).ToList();
            try { await Task.WhenAll(tasks); } catch (Exception ex) { Debug.WriteLine($"Error in UpdateAllMapRunningStatusesAsync: {ex.Message}"); }
            StatusBarText.Text = "Map running statuses updated."; // Consider localizing
        }

        private async void ReloadMapsListButton_Click(object sender, RoutedEventArgs e) { await RefreshMapsListAndStatusAsync(); }

        private async void SaveMapConfigurationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mapManagerService == null) { MessageBox.Show("Map service not initialized.", "Error"); return; } // Consider localizing
            try
            {
                List<MapConfiguration> configsToSave = DisplayableMaps.Select(dm => dm.Config).ToList();
                await _mapManagerService.SaveMapConfigurationsAsync(configsToSave);
                StatusBarText.Text = "Map configurations saved to local file."; // Consider localizing
                MessageBox.Show("Map configurations saved locally!", "Success"); // Consider localizing
            }
            catch (Exception ex) { StatusBarText.Text = $"Error saving map settings: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error saving map settings to local file: {ex.Message}", "Map Save Error", MessageBoxButton.OK, MessageBoxImage.Error); } // Consider localizing
        }

        private async void StartIndividualMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mapManagerService == null || _daemonService == null || !_daemonService.IsConnected) { MessageBox.Show("Not connected to daemon or map service not initialized.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing. Cannot start map.", "API Key Error"); return; } // Consider localizing

            if (sender is Button button && button.CommandParameter is MapDisplayInfo mapToStart)
            {
                StatusBarText.Text = $"Starting map {mapToStart.MapId} via daemon..."; // Consider localizing
                try
                {
                    if (mapToStart.IsMainWorldServer) { await LoadMapsAndStartMainGameServerAsync(true); }
                    else { await _mapManagerService.StartMapAsync(mapToStart.MapId); }
                    await Task.Delay(1000); await UpdateSingleMapRunningStatusAsync(mapToStart);
                    StatusBarText.Text = $"Start command sent for map {mapToStart.MapId} to daemon."; // Consider localizing
                }
                catch (Exception ex) { StatusBarText.Text = $"Error starting map {mapToStart.MapId} via daemon: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error starting map {mapToStart.MapId} via daemon: {ex.Message}", "Map Error", MessageBoxButton.OK, MessageBoxImage.Error); await UpdateSingleMapRunningStatusAsync(mapToStart); } // Consider localizing
            }
        }

        private async void StopIndividualMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mapManagerService == null || _daemonService == null || !_daemonService.IsConnected) { MessageBox.Show("Not connected to daemon or map service not initialized.", "Error"); return; } // Consider localizing
            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey)) { MessageBox.Show("API Key is missing. Cannot stop map.", "API Key Error"); return; } // Consider localizing

            if (sender is Button button && button.CommandParameter is MapDisplayInfo mapToStop)
            {
                StatusBarText.Text = $"Stopping map {mapToStop.MapId} via daemon..."; // Consider localizing
                try
                {
                    await _mapManagerService.StopMapAsync(mapToStop.MapId);
                    await Task.Delay(1000);
                    await UpdateSingleMapRunningStatusAsync(mapToStop);
                    StatusBarText.Text = $"Stop command sent for map {mapToStop.MapId} to daemon."; // Consider localizing
                }
                catch (Exception ex) { StatusBarText.Text = $"Error stopping map {mapToStop.MapId} via daemon: {ex.Message.Split('\n')[0]}"; MessageBox.Show($"Error stopping map {mapToStop.MapId} via daemon: {ex.Message}", "Map Error", MessageBoxButton.OK, MessageBoxImage.Error); await UpdateSingleMapRunningStatusAsync(mapToStop); } // Consider localizing
            }
        }

        private void AddNewMapButton_Click(object sender, RoutedEventArgs e)
        {
            var newMapConfig = new MapConfiguration(false, "new_map_id" + DisplayableMaps.Count.ToString(), "New Map (Edit Me)"); // Consider localizing "New Map (Edit Me)"
            var newMapDisplayInfo = new MapDisplayInfo(newMapConfig);
            DisplayableMaps.Add(newMapDisplayInfo);
            if (this.FindName("MapsDataGrid") is DataGrid mapsDataGrid) { mapsDataGrid.SelectedItem = newMapDisplayInfo; mapsDataGrid.ScrollIntoView(newMapDisplayInfo); } // Ensure MapsDataGrid x:Name exists
            StatusBarText.Text = "New map added to list. Edit details and save configurations."; // Consider localizing
        }

        private void DeleteMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.FindName("MapsDataGrid") is DataGrid mapsDataGrid && mapsDataGrid.SelectedItem is MapDisplayInfo selectedMap) // Ensure MapsDataGrid x:Name exists
            {
                var result = MessageBox.Show($"Are you sure you want to delete the map '{selectedMap.MapName} ({selectedMap.MapId})' from the local list? This action cannot be undone from the UI once saved.", "Confirm Delete Map", MessageBoxButton.YesNo, MessageBoxImage.Warning); // Consider localizing
                if (result == MessageBoxResult.Yes) { DisplayableMaps.Remove(selectedMap); StatusBarText.Text = $"Map '{selectedMap.MapName}' removed from local list. Save configurations to persist changes."; } // Consider localizing
            }
            else { MessageBox.Show("Please select a map to delete from the list.", "No Map Selected", MessageBoxButton.OK, MessageBoxImage.Information); } // Consider localizing
        }

        private void LoadPresetsFromFiles()
        {
            ProcessConfigPresets.Clear();
            var presets = PresetManager.LoadAllPresets();
            foreach (var preset in presets)
            {
                ProcessConfigPresets.Add(preset);
            }
        }

        private static List<ProcessConfiguration> GetDefaultProcessConfigurations()
        {
            return new List<ProcessConfiguration> {
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

        private void UpdateConnectionStatus(bool isConnected, string statusText = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (ConnectionIndicator != null)
                {
                    ConnectionIndicator.Fill = isConnected 
                        ? Application.Current.FindResource("ModernSuccessBrush") as Brush 
                        : Application.Current.FindResource("ModernErrorBrush") as Brush;
                }
                
                if (ConnectionStatusText != null)
                {
                    ConnectionStatusText.Text = statusText ?? (isConnected ? "Connected" : "Disconnected");
                }
            });
        }
        
        private void UpdateServiceCounts()
        {
            Dispatcher.Invoke(() =>
            {
                if (RunningCountText != null && StoppedCountText != null)
                {
                    var runningCount = ServerProcesses.Count(p => p.Status == ProcessStatus.Running);
                    var stoppedCount = ServerProcesses.Count(p => p.Status == ProcessStatus.Stopped);
                    
                    RunningCountText.Text = runningCount.ToString();
                    StoppedCountText.Text = stoppedCount.ToString();
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // ADDED for Localization: Save settings on close
            SettingsManager.SaveSettings(AppSettings);

            _characterEditorViewModel?.Cleanup();
            _daemonService?.Dispose();
            _dbService?.Dispose(); // Assuming DatabaseService implements IDisposable
            base.OnClosed(e);
        }
    }
}