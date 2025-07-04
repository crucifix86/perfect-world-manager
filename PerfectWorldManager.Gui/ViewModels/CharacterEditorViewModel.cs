// PerfectWorldManager.Gui/ViewModels/CharacterEditorViewModel.cs
using PerfectWorldManager.Core;
using PerfectWorldManager.Grpc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using System.Linq;
using PerfectWorldManager.Gui.Utils;
using System.Windows;
using PerfectWorldManager.Core.Models;
using PerfectWorldManager.Core.Services;

namespace PerfectWorldManager.Gui.ViewModels
{
    public class CharacterEditorViewModel : ObservableObject
    {
        private readonly DaemonGrpcService _daemonService;
        private readonly Settings _settings;
        private readonly IItemLookupService _itemLookupService;

        private string _characterId = string.Empty;
        public string CharacterId
        {
            get => _characterId;
            set
            {
                if (SetProperty(ref _characterId, value, nameof(CharacterId)))
                {
                    UpdateCommandStates();
                }
            }
        }

        private string _characterXml = string.Empty;
        public string CharacterXml
        {
            get => _characterXml;
            set
            {
                if (SetProperty(ref _characterXml, value, nameof(CharacterXml)))
                {
                    // Synchronization logic handled by commands or tab changes
                }
            }
        }

        private CharacterRoleVm? _currentCharacterVm = null;
        public CharacterRoleVm? CurrentCharacterVm
        {
            get => _currentCharacterVm;
            set => SetProperty(ref _currentCharacterVm, value);
        }

        private InventoryItemVm? _selectedInventoryItem = null;
        public InventoryItemVm? SelectedInventoryItem
        {
            get => _selectedInventoryItem;
            set => SetProperty(ref _selectedInventoryItem, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value, nameof(IsLoading)))
                {
                    UpdateCommandStates();
                }
            }
        }

        private string _searchPlayerIdText = string.Empty;
        public string SearchPlayerIdText
        {
            get => _searchPlayerIdText;
            set
            {
                if (SetProperty(ref _searchPlayerIdText, value, nameof(SearchPlayerIdText)))
                {
                    (SearchPlayerCharactersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<GuiPlayerCharacterInfo> PlayerCharactersList { get; } = new ObservableCollection<GuiPlayerCharacterInfo>();

        private string _playerCharactersStatusMessage = string.Empty;
        public string PlayerCharactersStatusMessage
        {
            get => _playerCharactersStatusMessage;
            set => SetProperty(ref _playerCharactersStatusMessage, value, nameof(PlayerCharactersStatusMessage));
        }

        // All Characters List Properties
        public ObservableCollection<GuiPlayerCharacterInfo> AllCharactersList { get; } = new ObservableCollection<GuiPlayerCharacterInfo>();

        private string _allCharactersStatusMessage = string.Empty;
        public string AllCharactersStatusMessage
        {
            get => _allCharactersStatusMessage;
            set => SetProperty(ref _allCharactersStatusMessage, value, nameof(AllCharactersStatusMessage));
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value, nameof(CurrentPage));
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value, nameof(TotalPages));
        }

        private bool _canGoPrevious;
        public bool CanGoPrevious
        {
            get => _canGoPrevious;
            set => SetProperty(ref _canGoPrevious, value, nameof(CanGoPrevious));
        }

        private bool _canGoNext;
        public bool CanGoNext
        {
            get => _canGoNext;
            set => SetProperty(ref _canGoNext, value, nameof(CanGoNext));
        }

        private const int PageSize = 50;
        private const int MinCharacterId = 1024;
        private const int MaxCharacterId = 9000000;

        public ICommand LoadCharacterCommand { get; }
        public ICommand SaveCharacterCommand { get; }
        public ICommand SelectInventoryItemCommand { get; }
        public ICommand SyncGuiToXmlCommand { get; }
        public ICommand SyncXmlToGuiCommand { get; }
        public ICommand RefreshCharacterCommand { get; }
        public ICommand SearchPlayerCharactersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand RefreshAllCharactersCommand { get; }

        public CharacterEditorViewModel(DaemonGrpcService daemonService, Settings settings, IItemLookupService itemLookupService)
        {
            _daemonService = daemonService ?? throw new ArgumentNullException(nameof(daemonService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _itemLookupService = itemLookupService ?? throw new ArgumentNullException(nameof(itemLookupService));

            // Ensure IsLoading is false on startup
            _isLoading = false;

            if (_daemonService != null)
            {
                _daemonService.ConnectionEstablished += OnDaemonConnectionChanged;
                _daemonService.Disconnected += OnDaemonConnectionChanged;
                _daemonService.ConnectionFailed += OnDaemonConnectionStatusFailed;
            }

            LoadCharacterCommand = new RelayCommand(async param => await ExecuteLoadCharacterAsync(), param => CanExecuteLoadCharacter());
            SaveCharacterCommand = new RelayCommand(async param => await ExecuteSaveCharacterAsync(), param => CanExecuteSaveCharacter());
            SelectInventoryItemCommand = new RelayCommand(param => ExecuteSelectInventoryItem(param as InventoryItemVm), param => param is InventoryItemVm);

            SyncGuiToXmlCommand = new RelayCommand(param => ExecuteSyncGuiToXml(), param => CurrentCharacterVm != null);
            SyncXmlToGuiCommand = new RelayCommand(param => ExecuteSyncXmlToGui(), param => !string.IsNullOrWhiteSpace(CharacterXml));
            RefreshCharacterCommand = new RelayCommand(async param => await ExecuteRefreshCharacterAsync(), param => CanExecuteRefreshCharacter());

            // Corrected RelayCommand instantiation
            SearchPlayerCharactersCommand = new RelayCommand(async _ => await ExecuteSearchPlayerCharactersAsync(), _ => CanExecuteSearchPlayerCharacters());
            
            // Pagination commands
            PreviousPageCommand = new RelayCommand(_ => ExecutePreviousPage(), _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => ExecuteNextPage(), _ => CanGoNext);
            RefreshAllCharactersCommand = new RelayCommand(async _ => await LoadAllCharactersPageAsync(), _ => !IsLoading && _daemonService != null && _daemonService.IsConnected);
        }

        private void OnDaemonConnectionChanged(object? sender, EventArgs e) // Made sender nullable
        {
            UpdateCommandStates();
        }

        private void OnDaemonConnectionStatusFailed(object? sender, string e) // Made sender nullable
        {
            UpdateCommandStates();
        }

        private void ExecuteSelectInventoryItem(InventoryItemVm? item) // Made item nullable
        {
            SelectedInventoryItem = item;
        }

        private void ExecuteSyncGuiToXml()
        {
            if (CurrentCharacterVm != null)
            {
                try
                {
                    CharacterXml = CharacterXmlParser.Serialize(CurrentCharacterVm);
                    StatusMessage = "GUI data synchronized to Raw XML view.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error synchronizing GUI to XML: {ex.Message}";
                    Debug.WriteLine($"Error in ExecuteSyncGuiToXml: {ex}");
                }
            }
        }

        private void ExecuteSyncXmlToGui()
        {
            if (!string.IsNullOrWhiteSpace(CharacterXml))
            {
                try
                {
                    CurrentCharacterVm = CharacterXmlParser.Parse(CharacterXml, _settings, _itemLookupService);
                    StatusMessage = "Raw XML data parsed to GUI view.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error parsing Raw XML for GUI: {ex.Message}";
                    Debug.WriteLine($"Error in ExecuteSyncXmlToGui: {ex}");
                    CurrentCharacterVm = null;
                }
            }
            UpdateCommandStates();
        }

        private bool CanExecuteLoadCharacter() => !IsLoading && !string.IsNullOrWhiteSpace(CharacterId) && _daemonService != null && _daemonService.IsConnected;
        private bool CanExecuteSaveCharacter() => !IsLoading && (!string.IsNullOrWhiteSpace(CharacterXml) || CurrentCharacterVm != null) && !string.IsNullOrWhiteSpace(CharacterId) && _daemonService != null && _daemonService.IsConnected;
        private bool CanExecuteRefreshCharacter() => CanExecuteLoadCharacter();

        private async Task ExecuteLoadCharacterAsync()
        {
            if (!CanExecuteLoadCharacter()) return;

            IsLoading = true;
            StatusMessage = "Loading character data...";
            CharacterXml = string.Empty;
            CurrentCharacterVm = null;
            SelectedInventoryItem = null;

            try
            {
                var request = new ExportCharacterRequest { CharacterId = this.CharacterId };
                var response = await _daemonService.ExportCharacterDataAsync(request);

                if (response != null && response.Success && response.XmlData != null)
                {
                    CharacterXml = PrettifyXml(response.XmlData);
                    CurrentCharacterVm = CharacterXmlParser.Parse(CharacterXml, _settings, _itemLookupService);
                    StatusMessage = $"Character {CharacterId} loaded successfully.";
                }
                else
                {
                    StatusMessage = $"Error loading character: {response?.Message ?? "No response from daemon."}";
                    Debug.WriteLine($"Failed to load character {CharacterId}: {response?.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Client-side exception while loading character: {ex.Message}";
                Debug.WriteLine($"Exception in ExecuteLoadCharacterAsync for {CharacterId}: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExecuteRefreshCharacterAsync()
        {
            StatusMessage = "Refreshing character data...";
            await ExecuteLoadCharacterAsync();
        }

        private async Task ExecuteSaveCharacterAsync()
        {
            if (!CanExecuteSaveCharacter()) return;

            IsLoading = true;
            StatusMessage = "Saving character data...";

            try
            {
                string xmlToSave;

                if (CurrentCharacterVm != null)
                {
                    xmlToSave = CharacterXmlParser.Serialize(CurrentCharacterVm);
                }
                else if (!string.IsNullOrWhiteSpace(CharacterXml))
                {
                    xmlToSave = CharacterXml;
                }
                else
                {
                    StatusMessage = "No character data to save.";
                    IsLoading = false;
                    return;
                }

                if (string.IsNullOrWhiteSpace(xmlToSave))
                {
                    StatusMessage = "XML data is empty or invalid after processing.";
                    IsLoading = false;
                    return;
                }

                var request = new ImportCharacterRequest
                {
                    CharacterId = this.CharacterId,
                    XmlData = xmlToSave
                };
                var response = await _daemonService.ImportCharacterDataAsync(request);

                if (response != null && response.Success)
                {
                    StatusMessage = $"Character {CharacterId} saved successfully. {response.Message}";
                }
                else
                {
                    StatusMessage = $"Error saving character: {response?.Message ?? "No response from daemon."}";
                    Debug.WriteLine($"Failed to save character {CharacterId}: {response?.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Client-side exception while saving character: {ex.Message}";
                Debug.WriteLine($"Exception in ExecuteSaveCharacterAsync for {CharacterId}: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanExecuteSearchPlayerCharacters()
        {
            return !IsLoading &&
                   !string.IsNullOrWhiteSpace(SearchPlayerIdText) &&
                   int.TryParse(SearchPlayerIdText, out _) &&
                   _daemonService != null &&
                   _daemonService.IsConnected;
        }

        private async Task ExecuteSearchPlayerCharactersAsync()
        {
            if (!CanExecuteSearchPlayerCharacters()) return;

            if (!int.TryParse(SearchPlayerIdText, out int userId))
            {
                PlayerCharactersStatusMessage = "Invalid Player User ID format. Please enter a number.";
                return;
            }

            IsLoading = true;
            PlayerCharactersStatusMessage = "Searching for player characters...";
            PlayerCharactersList.Clear();

            try
            {
                var (characters, success, message) = await _daemonService.GetPlayerCharactersAsync(userId);
                if (success)
                {
                    if (characters != null && characters.Any())
                    {
                        foreach (var character in characters)
                        {
                            PlayerCharactersList.Add(character);
                        }
                        PlayerCharactersStatusMessage = $"Found {characters.Count} character(s).";
                    }
                    else
                    {
                        PlayerCharactersStatusMessage = "No characters found for this Player User ID.";
                    }
                }
                else
                {
                    PlayerCharactersStatusMessage = $"Error searching characters: {message ?? "Unknown error"}";
                }
            }
            catch (Exception ex)
            {
                PlayerCharactersStatusMessage = $"Client-side exception: {ex.Message}";
                Debug.WriteLine($"Exception in ExecuteSearchPlayerCharactersAsync for User ID {userId}: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string PrettifyXml(string xmlData)
        {
            if (string.IsNullOrWhiteSpace(xmlData)) return xmlData;
            try
            {
                XDocument doc = XDocument.Parse(xmlData);
                return doc.ToString(SaveOptions.None);
            }
            catch (Exception)
            {
                return xmlData;
            }
        }

        private void ExecutePreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                _ = LoadAllCharactersPageAsync();
            }
        }

        private void ExecuteNextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                _ = LoadAllCharactersPageAsync();
            }
        }

        private async Task LoadAllCharactersPageAsync()
        {
            if (!_daemonService.IsConnected) return;

            IsLoading = true;
            AllCharactersStatusMessage = $"Loading page {CurrentPage}...";
            AllCharactersList.Clear();

            try
            {
                // Calculate the range of character IDs for this page
                int startId = MinCharacterId + ((CurrentPage - 1) * PageSize);
                int endId = Math.Min(startId + PageSize - 1, MaxCharacterId);

                // Get characters in this range
                var (characters, success, message) = await _daemonService.GetCharacterRangeAsync(startId, endId);
                
                if (success && characters != null)
                {
                    foreach (var character in characters)
                    {
                        AllCharactersList.Add(character);
                    }
                    
                    AllCharactersStatusMessage = $"Showing {characters.Count} characters";
                    
                    // Update pagination state
                    TotalPages = (int)Math.Ceiling((double)(MaxCharacterId - MinCharacterId + 1) / PageSize);
                    CanGoPrevious = CurrentPage > 1;
                    CanGoNext = CurrentPage < TotalPages;
                    
                    (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
                else
                {
                    AllCharactersStatusMessage = $"Error: {message ?? "Failed to load characters"}";
                }
            }
            catch (Exception ex)
            {
                AllCharactersStatusMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Exception in LoadAllCharactersPageAsync: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void UpdateCommandStates() // Removed 'new' keyword (CS0109)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                (LoadCharacterCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveCharacterCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SyncGuiToXmlCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SyncXmlToGuiCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshCharacterCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SearchPlayerCharactersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshAllCharactersCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        public void Cleanup()
        {
            if (_daemonService != null)
            {
                _daemonService.ConnectionEstablished -= OnDaemonConnectionChanged;
                _daemonService.Disconnected -= OnDaemonConnectionChanged;
                _daemonService.ConnectionFailed -= OnDaemonConnectionStatusFailed;
            }
        }
    }
}