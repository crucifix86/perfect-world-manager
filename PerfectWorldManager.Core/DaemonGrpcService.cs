// PerfectWorldManager.Core/DaemonGrpcService.cs
using Grpc.Net.Client;
using PerfectWorldManager.Grpc; // Namespace from your .proto file's csharp_namespace option
using System;
using System.Collections.Generic; // Added for List<>
using System.Threading.Tasks;
using Grpc.Core; // Required for RpcException and Metadata
using System.Net.Http; // Required for HttpClient and related classes
using System.Diagnostics; // For Debug.WriteLine

namespace PerfectWorldManager.Core
{
    // This class can be moved to a Models folder or kept here if preferred
    // It's used by the ViewModel to display character info.
    public class GuiPlayerCharacterInfo
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int Level { get; set; }
        public int UserId { get; set; }
    }

    public class DaemonGrpcService : IDisposable
    {
        private readonly Settings _settings;
        private GrpcChannel? _channel;
        private Manager.ManagerClient? _client;
        private HttpClient? _httpClient;
        private string _apiKey; // Store API Key

        private bool _isDisposed = false;
        private string _currentDaemonUrl = string.Empty;
        private string _currentApiKey = string.Empty; // To track API key changes

        public bool IsConnected { get; private set; }

        public event EventHandler? ConnectionAttempting;
        public event EventHandler? ConnectionEstablished;
        public event EventHandler<string>? ConnectionFailed;
        public event EventHandler? Disconnected;


        public DaemonGrpcService(Settings settings)
        {
            _settings = settings;
            _apiKey = settings.ApiKey; // Store API key from settings
        }

        private void InitializeChannelAndClient(bool forceReconnect = false)
        {
            if (_isDisposed) return;

            // Check if API key has changed, force reconnect if so
            if (_settings.ApiKey != _currentApiKey)
            {
                forceReconnect = true;
                _apiKey = _settings.ApiKey; // Update stored API key
            }

            if (!forceReconnect && _client != null && _channel != null && _settings.DaemonServiceUrl == _currentDaemonUrl && IsConnected)
            {
                return;
            }

            if (_channel != null)
            {
                try { _channel.ShutdownAsync().Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
                _channel.Dispose();
                _channel = null;
            }
            if (_httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }
            _client = null;
            IsConnected = false;

            _currentDaemonUrl = _settings.DaemonServiceUrl;
            _currentApiKey = _settings.ApiKey; // Store current API Key for change detection
            ConnectionAttempting?.Invoke(this, EventArgs.Empty);

            try
            {
                if (string.IsNullOrWhiteSpace(_currentDaemonUrl))
                {
                    throw new InvalidOperationException("Daemon service URL is not configured.");
                }
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    // It's better to let calls fail with Unauthenticated from server if API key is expected
                    // but if client-side validation is desired:
                    // throw new InvalidOperationException("API Key is not configured.");
                    Debug.WriteLine("Warning: API Key is not configured in client settings. Calls might be rejected by the daemon.");
                }


                var httpHandler = new HttpClientHandler();
                _httpClient = new HttpClient(httpHandler);

                var channelOptions = new GrpcChannelOptions
                {
                    HttpClient = _httpClient,
                    MaxReceiveMessageSize = null,
                    MaxSendMessageSize = null,
                };

                _channel = GrpcChannel.ForAddress(_currentDaemonUrl, channelOptions);
                _client = new Manager.ManagerClient(_channel);

                IsConnected = true; // Assume connected, actual calls will verify with API key
                ConnectionEstablished?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine($"gRPC Channel created for: {_currentDaemonUrl}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                string errorMessage = $"Failed to create gRPC channel for '{_currentDaemonUrl}': {ex.Message}";
                ConnectionFailed?.Invoke(this, errorMessage);
                Debug.WriteLine($"ERROR: {errorMessage} - Exception: {ex}");
                _client = null;
                if (_channel != null) { _channel.Dispose(); _channel = null; }
                if (_httpClient != null) { _httpClient.Dispose(); _httpClient = null; }
            }
        }

        public Manager.ManagerClient? GetClient(bool forceReconnect = false)
        {
            if (_isDisposed)
            {
                Debug.WriteLine("DaemonGrpcService is disposed. Cannot get client.");
                return null;
            }
            // Force re-init if API key changed
            if (_settings.ApiKey != _currentApiKey)
            {
                forceReconnect = true;
            }


            if (_client == null || _channel == null || _settings.DaemonServiceUrl != _currentDaemonUrl || forceReconnect || !IsConnected)
            {
                InitializeChannelAndClient(true); // Pass true to ensure re-initialization
            }
            return _client;
        }

        private Metadata GetApiKeyHeaders()
        {
            var headers = new Metadata();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                headers.Add("x-api-key", _apiKey);
            }
            return headers;
        }


        private async Task<TResponse> ExecuteGrpcCallAsync<TResponse>(Func<Manager.ManagerClient, CallOptions, Task<TResponse>> grpcCall, Func<TResponse> defaultErrorResponseFactory)
            where TResponse : class
        {
            var client = GetClient();
            if (client == null || !IsConnected) // IsConnected might be true but API key changed, GetClient handles re-init
            {
                // Re-check after GetClient attempt which might re-initialize
                client = GetClient(true); // Force re-check/re-init
                if (client == null || !IsConnected)
                {
                    string errorMsg = "gRPC client not initialized, not connected, or service disposed.";
                    Debug.WriteLine($"ERROR: {errorMsg}");
                    ConnectionFailed?.Invoke(this, errorMsg);
                    return defaultErrorResponseFactory();
                }
            }
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                string errorMsg = "API Key is missing. Please configure it in settings.";
                Debug.WriteLine($"ERROR: {errorMsg}");
                ConnectionFailed?.Invoke(this, errorMsg);
                return defaultErrorResponseFactory();
            }

            try
            {
                var headers = GetApiKeyHeaders();
                var callOptions = new CallOptions(headers, deadline: Deadline);
                TResponse response = await grpcCall(client, callOptions);
                return response;
            }
            catch (RpcException ex)
            {
                IsConnected = false;
                string errorMessage = $"gRPC Error: {ex.StatusCode}";
                if (ex.StatusCode == StatusCode.Unauthenticated)
                {
                    errorMessage = "Daemon authentication failed: Invalid or missing API Key.";
                }
                else if (!string.IsNullOrWhiteSpace(ex.Status.Detail)) errorMessage += $" - {ex.Status.Detail}";
                else if (!string.IsNullOrWhiteSpace(ex.Message)) errorMessage += $" - {ex.Message.Split(Environment.NewLine)[0]}";

                Debug.WriteLine($"ERROR: RpcException in ExecuteGrpcCallAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);

                if (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Internal || ex.StatusCode == StatusCode.Unauthenticated)
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
                return defaultErrorResponseFactory();
            }
            catch (Exception ex)
            {
                IsConnected = false;
                string errorMessage = $"Unexpected Error during gRPC call: {ex.Message.Split(Environment.NewLine)[0]}";
                Debug.WriteLine($"ERROR: Unexpected exception in ExecuteGrpcCallAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);
                Disconnected?.Invoke(this, EventArgs.Empty);
                return defaultErrorResponseFactory();
            }
        }

        public Task<ProcessResponse> StartProcessAsync(StartProcessRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.StartProcessAsync(request, options),
                                 () => new ProcessResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during StartProcess." });

        public Task<ProcessResponse> StopProcessAsync(StopProcessRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.StopProcessAsync(request, options),
                                 () => new ProcessResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during StopProcess." });

        public Task<ProcessStatusResponse> GetProcessStatusAsync(ProcessStatusRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.GetProcessStatusAsync(request, options),
                                 () => new ProcessStatusResponse { Status = ProcessStatusResponse.Types.Status.Error, Details = "Daemon Error: Failed to connect or client error during GetProcessStatus." });

        public Task<ProcessResponse> StartMapAsync(StartMapRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.StartMapAsync(request, options),
                                 () => new ProcessResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during StartMap." });

        public Task<ProcessResponse> StopMapAsync(StopMapRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.StopMapAsync(request, options),
                                 () => new ProcessResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during StopMap." });

        public Task<MapStatusResponse> GetMapStatusAsync(MapStatusRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.GetMapStatusAsync(request, options),
                                 () => new MapStatusResponse { IsRunning = false, Details = "Daemon Error: Failed to connect or client error during GetMapStatus." });

        public Task<ExecuteCommandResponse> ExecuteCommandAsync(ExecuteCommandRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.ExecuteCommandAsync(request, options),
                                 () => new ExecuteCommandResponse { Success = false, ErrorOutput = "Daemon Error: Failed to connect or client error during ExecuteCommand." });

        public Task<CharacterDataResponse> ExportCharacterDataAsync(ExportCharacterRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.ExportCharacterDataAsync(request, options),
                                 () => new CharacterDataResponse { Success = false, XmlData = string.Empty, Message = "Daemon Error: Failed to connect or client error during ExportCharacterData." });

        public Task<ImportCharacterResponse> ImportCharacterDataAsync(ImportCharacterRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.ImportCharacterDataAsync(request, options),
                                 () => new ImportCharacterResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during ImportCharacterData." });

        public async Task<(List<GuiPlayerCharacterInfo> Characters, bool Success, string Message)> GetCharacterRangeAsync(int startId, int endId)
        {
            var client = GetClient();
            if (client == null)
            {
                client = GetClient(true); // Force re-check
                if (client == null)
                {
                    return (new List<GuiPlayerCharacterInfo>(), false, "Daemon Error: Not connected or client unavailable.");
                }
            }
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                string errorMsg = "API Key is missing. Please configure it in settings.";
                ConnectionFailed?.Invoke(this, errorMsg);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMsg);
            }

            try
            {
                var request = new GetCharacterRangeRequest { StartId = startId, EndId = endId };
                var headers = GetApiKeyHeaders();
                var callOptions = new CallOptions(headers, deadline: Deadline);

                var response = await client.GetCharacterRangeAsync(request, callOptions);

                var guiCharacters = new List<GuiPlayerCharacterInfo>();
                if (response.Success)
                {
                    foreach (var charItem in response.Characters)
                    {
                        guiCharacters.Add(new GuiPlayerCharacterInfo 
                        { 
                            RoleId = charItem.RoleId, 
                            RoleName = charItem.RoleName,
                            Level = charItem.Level,
                            UserId = charItem.UserId
                        });
                    }
                }
                return (guiCharacters, response.Success, response.Message);
            }
            catch (RpcException ex)
            {
                IsConnected = false;
                string errorMessage = $"gRPC Error: {ex.StatusCode}";
                if (ex.StatusCode == StatusCode.Unauthenticated)
                {
                    errorMessage = "Daemon authentication failed: Invalid or missing API Key.";
                }
                else if (!string.IsNullOrWhiteSpace(ex.Status.Detail)) errorMessage += $" - {ex.Status.Detail}";
                else if (!string.IsNullOrWhiteSpace(ex.Message)) errorMessage += $" - {ex.Message.Split(Environment.NewLine)[0]}";
                Debug.WriteLine($"ERROR: RpcException in GetCharacterRangeAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);
                if (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Internal || ex.StatusCode == StatusCode.Unauthenticated) Disconnected?.Invoke(this, EventArgs.Empty);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMessage);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                string errorMessage = $"Client error in GetCharacterRangeAsync: {ex.Message.Split(Environment.NewLine)[0]}";
                Debug.WriteLine($"ERROR: Unexpected exception in GetCharacterRangeAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);
                Disconnected?.Invoke(this, EventArgs.Empty);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMessage);
            }
        }

        public async Task<(List<GuiPlayerCharacterInfo> Characters, bool Success, string Message)> GetPlayerCharactersAsync(int userId)
        {
            var client = GetClient();
            if (client == null)
            {
                client = GetClient(true); // Force re-check
                if (client == null)
                {
                    return (new List<GuiPlayerCharacterInfo>(), false, "Daemon Error: Not connected or client unavailable.");
                }
            }
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                string errorMsg = "API Key is missing. Please configure it in settings.";
                ConnectionFailed?.Invoke(this, errorMsg);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMsg);
            }


            try
            {
                var request = new GetPlayerCharactersRequest { UserId = userId };
                var headers = GetApiKeyHeaders();
                var callOptions = new CallOptions(headers, deadline: Deadline);

                var response = await client.GetPlayerCharactersAsync(request, callOptions);

                var guiCharacters = new List<GuiPlayerCharacterInfo>();
                if (response.Success)
                {
                    foreach (var charItem in response.Characters)
                    {
                        guiCharacters.Add(new GuiPlayerCharacterInfo 
                        { 
                            RoleId = charItem.RoleId, 
                            RoleName = charItem.RoleName,
                            Level = 0, // Level not provided by current gRPC response
                            UserId = userId 
                        });
                    }
                }
                return (guiCharacters, response.Success, response.Message);
            }
            catch (RpcException ex)
            {
                IsConnected = false;
                string errorMessage = $"gRPC Error: {ex.StatusCode}";
                if (ex.StatusCode == StatusCode.Unauthenticated)
                {
                    errorMessage = "Daemon authentication failed: Invalid or missing API Key.";
                }
                else if (!string.IsNullOrWhiteSpace(ex.Status.Detail)) errorMessage += $" - {ex.Status.Detail}";
                else if (!string.IsNullOrWhiteSpace(ex.Message)) errorMessage += $" - {ex.Message.Split(Environment.NewLine)[0]}";
                Debug.WriteLine($"ERROR: RpcException in GetPlayerCharactersAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);
                if (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Internal || ex.StatusCode == StatusCode.Unauthenticated) Disconnected?.Invoke(this, EventArgs.Empty);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMessage);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                string errorMessage = $"Client error in GetPlayerCharactersAsync: {ex.Message.Split(Environment.NewLine)[0]}";
                Debug.WriteLine($"ERROR: Unexpected exception in GetPlayerCharactersAsync: {errorMessage}\nFull Exception: {ex}");
                ConnectionFailed?.Invoke(this, errorMessage);
                Disconnected?.Invoke(this, EventArgs.Empty);
                return (new List<GuiPlayerCharacterInfo>(), false, errorMessage);
            }
        }

        // ----- Account Management Client Methods -----
        public Task<AccountActionResponse> CreateAccountAsync(CreateAccountRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.CreateAccountAsync(request, options),
                                 () => new AccountActionResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during CreateAccount." });

        public Task<AccountActionResponse> ChangePasswordAsync(ChangePasswordRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.ChangePasswordAsync(request, options),
                                 () => new AccountActionResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during ChangePassword." });

        public Task<AccountActionResponse> AddCubiAsync(AddCubiRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.AddCubiAsync(request, options),
                                 () => new AccountActionResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during AddCubi." });

        public Task<GetAllUsersResponse> GetAllUsersAsync(GetAllUsersRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.GetAllUsersAsync(request, options),
                                 () => new GetAllUsersResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during GetAllUsers." });

        public Task<AccountActionResponse> SetGmStatusAsync(SetGmStatusRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.SetGmStatusAsync(request, options),
                                 () => new AccountActionResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during SetGmStatus." });

        public Task<AccountActionResponse> DeleteUserAsync(DeleteUserRequest request) =>
            ExecuteGrpcCallAsync(async (client, options) => await client.DeleteUserAsync(request, options),
                                 () => new AccountActionResponse { Success = false, Message = "Daemon Error: Failed to connect or client error during DeleteUser." });


        private DateTime? Deadline => DateTime.UtcNow.AddSeconds(30);


        public void Dispose()
        {
            if (_isDisposed) return;

            IsConnected = false;
            _isDisposed = true;
            Disconnected?.Invoke(this, EventArgs.Empty);

            if (_channel != null)
            {
                try
                {
                    var shutdownTask = _channel.ShutdownAsync();
                    if (!shutdownTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Debug.WriteLine("gRPC channel shutdown timed out.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during gRPC channel shutdown: {ex.Message}");
                }
                finally
                {
                    _channel.Dispose();
                    _channel = null;
                }
            }

            if (_httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }

            _client = null;
            GC.SuppressFinalize(this);
            Debug.WriteLine("DaemonGrpcService disposed.");
        }
    }
}