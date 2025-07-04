using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PerfectWorldManager.Core;
using PerfectWorldManager.Core.Utils;
using PerfectWorldManager.Gui.Services;

namespace PerfectWorldManager.Gui.Dialogs
{
    public partial class ProcessConfigWindow : Window
    {
        private readonly Settings _settings;
        private readonly MainWindow _mainWindow;
        
        public ObservableCollection<ProcessConfigurationPreset> ProcessConfigPresets { get; set; }
        public ObservableCollection<ProcessConfiguration> ProcessConfigurations { get; set; }
        public string ActivePresetName { get; set; }
        
        public ProcessConfigWindow(Settings settings, MainWindow mainWindow)
        {
            InitializeComponent();
            _settings = settings;
            _mainWindow = mainWindow;
            
            // Create a copy of the configurations to work with
            ProcessConfigurations = new ObservableCollection<ProcessConfiguration>();
            foreach (var config in settings.ProcessConfigurations)
            {
                ProcessConfigurations.Add(new ProcessConfiguration
                {
                    Type = config.Type,
                    DisplayName = config.DisplayName,
                    IsEnabled = config.IsEnabled,
                    ExecutableDir = config.ExecutableDir,
                    ExecutableName = config.ExecutableName,
                    StartArguments = config.StartArguments,
                    StatusCheckPattern = config.StatusCheckPattern,
                    MapId = config.MapId
                });
            }
            
            ProcessConfigPresets = mainWindow.ProcessConfigPresets;
            ActivePresetName = settings.ActivePresetName;
            
            DataContext = this;
        }
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Normal)
                    WindowState = WindowState.Maximized;
                else
                    WindowState = WindowState.Normal;
            }
            else
            {
                DragMove();
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Copy the configurations back to settings
            _settings.ProcessConfigurations.Clear();
            foreach (var config in ProcessConfigurations)
            {
                _settings.ProcessConfigurations.Add(config);
            }
            
            // Save settings
            SettingsManager.SaveSettings(_settings);
            
            // Update main window
            _mainWindow.InitializeServerProcessList();
            NotificationManager.ShowSuccess("Settings Saved", "Process configurations have been saved");
            
            DialogResult = true;
            Close();
        }
        
        private void PresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Update the active preset name when selection changes
            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox?.SelectedValue is string presetName)
            {
                ActivePresetName = presetName;
            }
            
            // Update button states and description
            if (comboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                var updateButton = this.FindName("UpdatePresetButton") as System.Windows.Controls.Button;
                var deleteButton = this.FindName("DeletePresetButton") as System.Windows.Controls.Button;
                var descriptionText = this.FindName("PresetDescriptionText") as System.Windows.Controls.TextBlock;
                
                if (updateButton != null) updateButton.IsEnabled = !selectedPreset.IsReadOnly;
                if (deleteButton != null) deleteButton.IsEnabled = !selectedPreset.IsReadOnly;
                if (descriptionText != null) descriptionText.Text = selectedPreset.Description;
            }
        }
        
        private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            var presetComboBox = this.FindName("PresetComboBox") as System.Windows.Controls.ComboBox;
            if (presetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                var preset = PresetManager.LoadPreset(selectedPreset.Name);
                if (preset != null)
                {
                    ProcessConfigurations.Clear();
                    foreach (var config in preset.Configurations)
                    {
                        ProcessConfigurations.Add(new ProcessConfiguration
                        {
                            Type = config.Type,
                            DisplayName = config.DisplayName,
                            IsEnabled = config.IsEnabled,
                            ExecutableDir = config.ExecutableDir,
                            ExecutableName = config.ExecutableName,
                            StartArguments = config.StartArguments,
                            StatusCheckPattern = config.StatusCheckPattern,
                            MapId = config.MapId
                        });
                    }
                    _settings.ActivePresetName = selectedPreset.Name;
                    ActivePresetName = selectedPreset.Name;
                    
                    MessageBox.Show($"Loaded preset '{selectedPreset.Name}'", "Preset Loaded", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        
        private void SavePresetAsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Save Preset As", "Enter a name for the new preset:", "");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                var newPreset = new ProcessConfigurationPreset
                {
                    Name = dialog.ResponseText,
                    Description = $"Custom configuration saved on {DateTime.Now:yyyy-MM-dd}",
                    Configurations = new List<ProcessConfiguration>()
                };
                
                foreach (var config in ProcessConfigurations)
                {
                    newPreset.Configurations.Add(new ProcessConfiguration
                    {
                        Type = config.Type,
                        DisplayName = config.DisplayName,
                        IsEnabled = config.IsEnabled,
                        ExecutableDir = config.ExecutableDir,
                        ExecutableName = config.ExecutableName,
                        StartArguments = config.StartArguments,
                        StatusCheckPattern = config.StatusCheckPattern,
                        MapId = config.MapId
                    });
                }
                
                PresetManager.SavePreset(newPreset);
                _mainWindow.LoadPresetsFromFiles();
                ProcessConfigPresets = _mainWindow.ProcessConfigPresets;
                var presetComboBox = this.FindName("PresetComboBox") as System.Windows.Controls.ComboBox;
                if (presetComboBox != null) presetComboBox.SelectedValue = newPreset.Name;
                _settings.ActivePresetName = newPreset.Name;
                ActivePresetName = newPreset.Name;
                
                MessageBox.Show($"Saved new preset '{newPreset.Name}'", "Preset Saved", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void UpdatePresetButton_Click(object sender, RoutedEventArgs e)
        {
            var presetComboBox = this.FindName("PresetComboBox") as System.Windows.Controls.ComboBox;
            if (presetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                if (selectedPreset.IsReadOnly)
                {
                    MessageBox.Show("Cannot update read-only presets.", "Read-Only Preset", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var result = MessageBox.Show(
                    $"Are you sure you want to update preset '{selectedPreset.Name}' with the current configuration?",
                    "Update Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    selectedPreset.Configurations.Clear();
                    foreach (var config in ProcessConfigurations)
                    {
                        selectedPreset.Configurations.Add(new ProcessConfiguration
                        {
                            Type = config.Type,
                            DisplayName = config.DisplayName,
                            IsEnabled = config.IsEnabled,
                            ExecutableDir = config.ExecutableDir,
                            ExecutableName = config.ExecutableName,
                            StartArguments = config.StartArguments,
                            StatusCheckPattern = config.StatusCheckPattern,
                            MapId = config.MapId
                        });
                    }
                    selectedPreset.LastModifiedDate = DateTime.Now;
                    
                    PresetManager.SavePreset(selectedPreset);
                    MessageBox.Show($"Updated preset '{selectedPreset.Name}'", "Preset Updated", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        
        private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            var presetComboBox = this.FindName("PresetComboBox") as System.Windows.Controls.ComboBox;
            if (presetComboBox?.SelectedItem is ProcessConfigurationPreset selectedPreset)
            {
                if (selectedPreset.IsReadOnly)
                {
                    MessageBox.Show("Cannot delete read-only presets.", "Read-Only Preset", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var result = MessageBox.Show(
                    $"Are you sure you want to delete preset '{selectedPreset.Name}'?",
                    "Delete Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    PresetManager.DeletePreset(selectedPreset.Name);
                    
                    if (_settings.ActivePresetName == selectedPreset.Name)
                    {
                        _settings.ActivePresetName = "15x";
                        ActivePresetName = "15x";
                        LoadPresetButton_Click(sender, e);
                    }
                    
                    _mainWindow.LoadPresetsFromFiles();
                    ProcessConfigPresets = _mainWindow.ProcessConfigPresets;
                    var comboBox = this.FindName("PresetComboBox") as System.Windows.Controls.ComboBox;
                    if (comboBox != null) comboBox.SelectedValue = ActivePresetName;
                    
                    MessageBox.Show($"Deleted preset '{selectedPreset.Name}'", "Preset Deleted", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}