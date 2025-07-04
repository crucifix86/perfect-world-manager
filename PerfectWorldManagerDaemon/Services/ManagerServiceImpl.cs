// PerfectWorldManagerDaemon/Services/ManagerServiceImpl.cs
using Grpc.Core;
using PerfectWorldManager.Grpc;
using PerfectWorldManager.Core;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic; // For List

namespace PerfectWorldManagerDaemon.Services
{
    public class ManagerServiceImpl : Manager.ManagerBase
    {
        private readonly ILogger<ManagerServiceImpl> _logger;
        private readonly IConfiguration _configuration;
        private readonly DatabaseService _dbService; // Kept for other potential direct DB access methods
        private readonly Settings _settings; // Client-side settings, potentially for reference if needed by daemon logic not covered by appsettings.json

        private readonly string _serverBaseDir;
        private readonly string _originalGameExecutableBaseDir;
        private readonly string _characterEditorGameDbDir;
        private readonly string _pwAdminDir;
        private readonly string _logsDir;
        private readonly string _expectedApiKey; // Added for API Key validation

        public ManagerServiceImpl(ILogger<ManagerServiceImpl> logger, IConfiguration configuration, Settings settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings)); // Keep injected client settings for other potential non-DB uses

            // Load expected API Key from daemon's configuration
            _expectedApiKey = _configuration.GetValue<string>("Security:ApiKey");
            if (string.IsNullOrWhiteSpace(_expectedApiKey))
            {
                _logger.LogWarning("API Key is not configured in daemon's appsettings.json (Security:ApiKey).");
            }
            else
            {
                _logger.LogInformation("Daemon API Key loaded.");
            }

            // Create a new Settings object specifically for the DatabaseService, populated from daemon's IConfiguration
            var daemonDbSettings = new Settings
            {
                MySqlHost = _configuration.GetValue<string>("Database:MySqlHost"),
                MySqlPort = _configuration.GetValue<int>("Database:MySqlPort", 3306),
                MySqlUser = _configuration.GetValue<string>("Database:MySqlUser"),
                MySqlPassword = _configuration.GetValue<string>("Database:MySqlPassword"),
                MySqlDatabase = _configuration.GetValue<string>("Database:MySqlDatabase")
                // Other Settings properties will be default, which is fine as DatabaseService only uses these
            };

            if (string.IsNullOrWhiteSpace(daemonDbSettings.MySqlHost) ||
                string.IsNullOrWhiteSpace(daemonDbSettings.MySqlUser) ||
                string.IsNullOrWhiteSpace(daemonDbSettings.MySqlDatabase))
            {
                _logger.LogCritical("Daemon's database connection settings (Host, User, or DatabaseName) are missing in its appsettings.json (under Database section). DatabaseService may not function correctly for account operations.");
            }
            else
            {
                _logger.LogInformation($"ManagerServiceImpl: DatabaseService for daemon initialized using daemon's appsettings.json. " +
                                   $"MySqlHost: '{daemonDbSettings.MySqlHost}', MySqlUser: '{daemonDbSettings.MySqlUser}', " +
                                   $"MySqlDatabase: '{daemonDbSettings.MySqlDatabase}', MySqlPort: {daemonDbSettings.MySqlPort}. " +
                                   $"MySqlPassword is {(string.IsNullOrWhiteSpace(daemonDbSettings.MySqlPassword) ? "NOT SET" : "SET")}.");
            }

            _dbService = new DatabaseService(daemonDbSettings); // Use daemon-specific settings for DB connection

            _serverBaseDir = _configuration.GetValue<string>("PerfectWorldPaths:ServerDir") ?? "/home/pw_server_default_fallback";
            _originalGameExecutableBaseDir = _configuration.GetValue<string>("PerfectWorldPaths:GameExecutableBaseDir") ?? Path.Combine(_serverBaseDir, "gamed");
            _characterEditorGameDbDir = _configuration.GetValue<string>("PerfectWorldPaths:CharacterEditorGameDbDir") ?? "/invalid_path_character_editor_gamedbdir_not_set";
            _pwAdminDir = _configuration.GetValue<string>("PerfectWorldPaths:PwAdminDir") ?? Path.Combine(_serverBaseDir, "pwadmin_utils_fallback");
            _logsDir = _configuration.GetValue<string>("PerfectWorldPaths:LogsDir") ?? Path.Combine(_serverBaseDir, "daemon_managed_logs_fallback");

            _logger.LogInformation($"Server Base Directory: '{_serverBaseDir}'");
            _logger.LogInformation($"Original Game Executable Base Directory: '{_originalGameExecutableBaseDir}'");
            _logger.LogInformation($"Character Editor gamedbd Directory: '{_characterEditorGameDbDir}'");
            _logger.LogInformation($"PwAdmin Directory: '{_pwAdminDir}'");
            _logger.LogInformation($"Daemon Logs Directory: '{_logsDir}'");

            try
            {
                Action<string, string> ensureDir = (path, name) => {
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("/") && !Directory.Exists(path))
                    {
                        _logger.LogWarning($"{name} directory '{path}' does not exist. Attempting to create.");
                        Directory.CreateDirectory(path);
                    }
                };

                ensureDir(_serverBaseDir, "Server Base");
                ensureDir(_originalGameExecutableBaseDir, "Original Game Executable Base");
                if (_characterEditorGameDbDir != "/invalid_path_character_editor_gamedbdir_not_set")
                    ensureDir(_characterEditorGameDbDir, "Character Editor GameDbDir");
                ensureDir(_pwAdminDir, "PwAdmin");
                ensureDir(_logsDir, "Logs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Critical error ensuring directory structure. Please check permissions and paths in appsettings.json.");
            }
        }

        // API Key Validation Helper
        private bool IsApiKeyValid(ServerCallContext context)
        {
            var apiKeyHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "x-api-key");
            string? receivedApiKey = apiKeyHeader?.Value;

            if (string.IsNullOrWhiteSpace(_expectedApiKey))
            {
                // If daemon has no API key configured, allow requests (or deny if strict policy)
                // For this example, we'll allow if no key is configured on daemon, but log it.
                // If a key IS configured on daemon, client MUST send it.
                _logger.LogDebug("No API key configured on daemon. Allowing request without API key check.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(receivedApiKey))
            {
                _logger.LogWarning("API Key was not provided by the client.");
                context.Status = new Status(StatusCode.Unauthenticated, "API Key is required.");
                return false;
            }

            if (receivedApiKey != _expectedApiKey)
            {
                _logger.LogWarning($"Invalid API Key received from client: '{receivedApiKey}'");
                context.Status = new Status(StatusCode.Unauthenticated, "Invalid API Key.");
                return false;
            }
            _logger.LogDebug("API Key validated successfully.");
            return true;
        }


        public override Task<ProcessResponse> StartProcess(StartProcessRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ProcessResponse { Success = false, Message = context.Status.Detail });
            _logger.LogInformation($"Attempting to start process. Key: '{request.ProcessKey}', Executable: '{request.ExecutableName}', Args: '{request.Arguments}', WD: '{request.WorkingDirectory}'");
            var response = new ProcessResponse { Success = false };

            try
            {
                string effectiveWorkingDirectory = request.WorkingDirectory;
                string executableToRun = request.ExecutableName;

                if (string.IsNullOrWhiteSpace(effectiveWorkingDirectory))
                {
                    effectiveWorkingDirectory = _serverBaseDir;
                    _logger.LogInformation($"No WorkingDirectory provided or it was empty/relative in request for '{request.ProcessKey}', defaulting to daemon's ServerDir: '{effectiveWorkingDirectory}'");
                }
                else if (!Path.IsPathRooted(effectiveWorkingDirectory))
                {
                    _logger.LogWarning($"WorkingDirectory '{effectiveWorkingDirectory}' for '{request.ProcessKey}' is relative. Resolving against ServerDir '{_serverBaseDir}'. Consider sending absolute paths from client.");
                    effectiveWorkingDirectory = Path.Combine(_serverBaseDir, effectiveWorkingDirectory);
                }


                if (!Directory.Exists(effectiveWorkingDirectory))
                {
                    response.Message = $"Error: Specified WorkingDirectory '{effectiveWorkingDirectory}' does not exist on the server for process '{request.ProcessKey}'.";
                    _logger.LogError(response.Message);
                    return Task.FromResult(response);
                }

                if (!executableToRun.StartsWith("/") && !executableToRun.StartsWith("./") && File.Exists(Path.Combine(effectiveWorkingDirectory, executableToRun)))
                {
                    executableToRun = "./" + executableToRun;
                }

                string bashCommand;
                string logFilePath = Path.Combine(_logsDir, $"{request.LogFileNameBase ?? request.ProcessKey}_{DateTime.UtcNow:yyyyMMddHHmmss}.log");

                bashCommand = $"cd \"{effectiveWorkingDirectory.Replace("\"", "\\\"")}\"; nohup {executableToRun} {request.Arguments} > \"{logFilePath.Replace("\"", "\\\"")}\" 2>&1 &";


                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashCommand.Replace("\"", "\\\"")}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Executing: /bin/bash -c \"{bashCommand}\"");

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        response.Message = "Failed to start bash process.";
                        _logger.LogError(response.Message);
                    }
                    else
                    {
                        response.Success = true;
                        response.Message = $"Process '{request.ExecutableName}' start command issued. Log: {logFilePath}";
                        _logger.LogInformation(response.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting process '{request.ProcessKey}' ('{request.ExecutableName}')");
                response.Message = $"Exception: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public override Task<ProcessResponse> StopProcess(StopProcessRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ProcessResponse { Success = false, Message = context.Status.Detail });
            _logger.LogInformation($"Attempting to stop process. Key: '{request.ProcessKey}', Pattern: '{request.StatusCheckPattern}'");
            var response = new ProcessResponse { Success = false };

            try
            {
                if (string.IsNullOrWhiteSpace(request.StatusCheckPattern))
                {
                    response.Message = "StatusCheckPattern cannot be empty for StopProcess.";
                    _logger.LogWarning(response.Message);
                    return Task.FromResult(response);
                }

                var pkillPsi = new ProcessStartInfo
                {
                    FileName = "pkill",
                    Arguments = $"-9 -f \"{request.StatusCheckPattern.Replace("\"", "\\\"")}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                _logger.LogInformation($"Executing: {pkillPsi.FileName} {pkillPsi.Arguments}");

                using (var process = Process.Start(pkillPsi))
                {
                    if (process == null)
                    {
                        response.Message = "Failed to start pkill process.";
                        _logger.LogError(response.Message);
                    }
                    else
                    {
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            _logger.LogInformation($"pkill for '{request.StatusCheckPattern}' successful. Processes signaled.");
                            response.Success = true;
                            response.Message = $"Stop command for '{request.StatusCheckPattern}' sent, process(es) signaled.";
                        }
                        else if (process.ExitCode == 1)
                        {
                            _logger.LogInformation($"pkill for '{request.StatusCheckPattern}' found no matching processes.");
                            response.Success = true;
                            response.Message = $"No processes found matching '{request.StatusCheckPattern}'.";
                        }
                        else
                        {
                            _logger.LogError($"Error stopping process '{request.StatusCheckPattern}' with pkill. ExitCode: {process.ExitCode}");
                            response.Message = $"pkill failed. ExitCode: {process.ExitCode}.";
                        }
                    }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                _logger.LogError(ex, $"'pkill' command not found. Ensure it is installed and in the PATH for the daemon user.");
                response.Message = "'pkill' command not found on server.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception stopping process with pattern '{request.StatusCheckPattern}'");
                response.Message = $"Exception: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public override Task<ProcessStatusResponse> GetProcessStatus(ProcessStatusRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ProcessStatusResponse { Status = ProcessStatusResponse.Types.Status.Error, Details = context.Status.Detail });
            _logger.LogInformation($"Getting status for process. Key: '{request.ProcessKey}', Pattern: '{request.StatusCheckPattern}'");
            var response = new ProcessStatusResponse { Status = ProcessStatusResponse.Types.Status.Unknown };

            try
            {
                if (string.IsNullOrWhiteSpace(request.StatusCheckPattern))
                {
                    response.Status = ProcessStatusResponse.Types.Status.Error;
                    response.Details = "StatusCheckPattern cannot be empty for GetProcessStatus.";
                    _logger.LogWarning(response.Details);
                    return Task.FromResult(response);
                }

                var pgrepPsi = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-f \"{request.StatusCheckPattern.Replace("\"", "\\\"")}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                };
                _logger.LogInformation($"Executing: {pgrepPsi.FileName} {pgrepPsi.Arguments}");

                using (var process = Process.Start(pgrepPsi))
                {
                    if (process == null)
                    {
                        response.Status = ProcessStatusResponse.Types.Status.Error;
                        response.Details = "Failed to start pgrep process.";
                        _logger.LogError(response.Details);
                    }
                    else
                    {
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            response.Status = ProcessStatusResponse.Types.Status.Running;
                            response.Details = $"Process is running (pgrep exit code 0 for pattern '{request.StatusCheckPattern}')";
                            _logger.LogInformation($"Process '{request.StatusCheckPattern}' is RUNNING.");
                        }
                        else if (process.ExitCode == 1)
                        {
                            response.Status = ProcessStatusResponse.Types.Status.Stopped;
                            response.Details = $"Process is not running (pgrep exit code 1 for pattern '{request.StatusCheckPattern}')";
                            _logger.LogInformation($"Process '{request.StatusCheckPattern}' is STOPPED (pgrep found no match).");
                        }
                        else
                        {
                            response.Status = ProcessStatusResponse.Types.Status.Error;
                            response.Details = $"Error checking status via pgrep. ExitCode: {process.ExitCode}.";
                            _logger.LogError($"pgrep failed for '{request.StatusCheckPattern}'. {response.Details}");
                        }
                    }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                _logger.LogError(ex, $"'pgrep' command not found. Ensure it is installed and in the PATH for the daemon user.");
                response.Status = ProcessStatusResponse.Types.Status.Error;
                response.Details = "'pgrep' command not found on server.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception getting status for pattern '{request.StatusCheckPattern}'");
                response.Status = ProcessStatusResponse.Types.Status.Error;
                response.Details = $"Exception: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public override Task<ProcessResponse> StartMap(StartMapRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ProcessResponse { Success = false, Message = context.Status.Detail });
            _logger.LogInformation($"Attempting to start map: '{request.MapId}', IsMainWithList: {request.IsMainWorldServerWithMapList}, Additional: {string.Join(",", request.AdditionalMapIds)}");
            var response = new ProcessResponse { Success = false };

            try
            {
                string gameServerWorkingDirectory = Path.Combine(_serverBaseDir, "gamed");
                if (!Directory.Exists(gameServerWorkingDirectory))
                {
                    gameServerWorkingDirectory = _originalGameExecutableBaseDir;
                    _logger.LogWarning($"Directory '{Path.Combine(_serverBaseDir, "gamed")}' not found. Using GameExecutableBaseDir '{_originalGameExecutableBaseDir}' as working directory for 'gs'.");
                }
                if (!Directory.Exists(gameServerWorkingDirectory))
                {
                    response.Message = $"Error: Game server working directory for 'gs' not found at '{Path.Combine(_serverBaseDir, "gamed")}' or '{_originalGameExecutableBaseDir}'.";
                    _logger.LogError(response.Message);
                    return Task.FromResult(response);
                }

                string gameServerExecutableName = "./gs";
                string arguments;

                if (request.IsMainWorldServerWithMapList && request.MapId.Equals("gs01", StringComparison.OrdinalIgnoreCase))
                {
                    string baseArgsForGs01 = "gs01 gs.conf gmserver.conf gsalias.conf";
                    arguments = request.AdditionalMapIds.Any()
                        ? $"{baseArgsForGs01} {string.Join(" ", request.AdditionalMapIds)}"
                        : baseArgsForGs01;
                }
                else
                {
                    arguments = request.MapId;
                }

                string logFileName = Path.Combine(_logsDir, $"map_{request.MapId.Replace(" ", "_").Replace("/", "_")}_{DateTime.UtcNow:yyyyMMddHHmmss}.log");
                string bashCommand = $"cd \"{gameServerWorkingDirectory.Replace("\"", "\\\"")}\"; nohup {gameServerExecutableName} {arguments} > \"{logFileName.Replace("\"", "\\\"")}\" 2>&1 &";

                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashCommand.Replace("\"", "\\\"")}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Executing: /bin/bash -c \"{bashCommand}\" (for map {request.MapId})");

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        response.Message = "Failed to start map process (Process.Start for bash returned null).";
                        _logger.LogError(response.Message);
                    }
                    else
                    {
                        response.Success = true;
                        response.Message = $"Start command for map '{request.MapId}' issued. Log: {logFileName}";
                        _logger.LogInformation(response.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting map '{request.MapId}'");
                response.Message = $"Exception: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public override Task<ProcessResponse> StopMap(StopMapRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ProcessResponse { Success = false, Message = context.Status.Detail });
            _logger.LogInformation($"Attempting to stop map: '{request.MapId}'");
            string gameServerExecutableNameOnly = "gs";
            string pattern = $"{gameServerExecutableNameOnly}[[:space:]]+{request.MapId}([[:space:]]|$)";

            _logger.LogInformation($"StopMap: Using pkill pattern: '{pattern}' for map '{request.MapId}'");
            // Call the base StopProcess which now includes API key check implicitly via its own call to IsApiKeyValid
            return StopProcess(new StopProcessRequest { ProcessKey = $"map_{request.MapId}", StatusCheckPattern = pattern }, context);
        }

        public override async Task<MapStatusResponse> GetMapStatus(MapStatusRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new MapStatusResponse { IsRunning = false, Details = context.Status.Detail };
            _logger.LogInformation($"Getting status for map: '{request.MapId}'");
            string gameServerExecutableNameOnly = "gs";
            string pattern = $"{gameServerExecutableNameOnly}[[:space:]]+{request.MapId}([[:space:]]|$)";
            _logger.LogInformation($"GetMapStatus: Using pgrep pattern: '{pattern}' for map '{request.MapId}'");

            var processStatusResponse = await GetProcessStatus(new ProcessStatusRequest { ProcessKey = $"map_{request.MapId}", StatusCheckPattern = pattern }, context);

            // If GetProcessStatus itself failed due to API key (though it shouldn't reach here if the outer check failed),
            // processStatusResponse.Details would already reflect that.
            return new MapStatusResponse
            {
                IsRunning = processStatusResponse.Status == ProcessStatusResponse.Types.Status.Running,
                Details = processStatusResponse.Details
            };
        }

        public override Task<ExecuteCommandResponse> ExecuteCommand(ExecuteCommandRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return Task.FromResult(new ExecuteCommandResponse { Success = false, ErrorOutput = context.Status.Detail });
            _logger.LogInformation($"Executing command: '{request.Command}', WD: '{request.WorkingDirectory}'");
            var response = new ExecuteCommandResponse { Success = false };

            try
            {
                string effectiveWorkingDirectory = request.WorkingDirectory;
                if (string.IsNullOrWhiteSpace(effectiveWorkingDirectory))
                {
                    effectiveWorkingDirectory = _serverBaseDir;
                    _logger.LogInformation($"No WorkingDirectory provided for ExecuteCommand, defaulting to ServerDir: '{effectiveWorkingDirectory}'");
                }
                else if (!Path.IsPathRooted(effectiveWorkingDirectory))
                {
                    _logger.LogWarning($"WorkingDirectory '{effectiveWorkingDirectory}' for ExecuteCommand is relative. Resolving against ServerDir '{_serverBaseDir}'.");
                    effectiveWorkingDirectory = Path.Combine(_serverBaseDir, effectiveWorkingDirectory);
                }

                if (!Directory.Exists(effectiveWorkingDirectory))
                {
                    response.ErrorOutput = $"Working directory '{effectiveWorkingDirectory}' does not exist on server.";
                    _logger.LogWarning(response.ErrorOutput);
                    return Task.FromResult(response);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{request.Command.Replace("\"", "\\\"")}\"",
                    WorkingDirectory = effectiveWorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                _logger.LogInformation($"Executing: {psi.FileName} {psi.Arguments} in WD: {psi.WorkingDirectory}");

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                int exitCode = -1;

                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (process.WaitForExit(30000))
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        response.Success = false;
                        response.ErrorOutput = "Command execution timed out after 30 seconds.";
                        _logger.LogWarning(response.ErrorOutput);
                        try { process.Kill(true); }
                        catch (Exception exKill) { _logger.LogError(exKill, "Failed to kill timed out process for ExecuteCommand."); }
                        return Task.FromResult(response);
                    }
                }

                response.ExitCode = exitCode;
                response.Success = (exitCode == 0);
                response.Output = outputBuilder.ToString().TrimEnd();
                response.ErrorOutput = errorBuilder.ToString().TrimEnd();

                if (response.Success)
                {
                    _logger.LogInformation($"Command executed successfully. ExitCode: {response.ExitCode}. Output (first 500 chars): {response.Output.Substring(0, Math.Min(response.Output.Length, 500))}");
                }
                else
                {
                    _logger.LogWarning($"Command failed. ExitCode: {response.ExitCode}. ErrorOutput: {response.ErrorOutput}. Output: {response.Output}");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                _logger.LogError(ex, $"'bash' command not found. Ensure it is installed and in the PATH for the daemon user.");
                response.ErrorOutput = "'bash' command not found on server.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception executing command: {request.Command}");
                response.ErrorOutput = $"Exception: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public override async Task<CharacterDataResponse> ExportCharacterData(ExportCharacterRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new CharacterDataResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to export character data for ID: {request.CharacterId} using GameDbDir: '{_characterEditorGameDbDir}'");
            var response = new CharacterDataResponse { Success = false };

            // ... (rest of the method remains the same)
            if (string.IsNullOrWhiteSpace(request.CharacterId))
            {
                response.Message = "Character ID cannot be empty.";
                _logger.LogWarning(response.Message);
                return response;
            }
            if (!int.TryParse(request.CharacterId, out _))
            {
                response.Message = "Invalid Character ID format.";
                _logger.LogWarning(response.Message);
                return response;
            }

            if (string.IsNullOrEmpty(_characterEditorGameDbDir) || _characterEditorGameDbDir == "/invalid_path_character_editor_gamedbdir_not_set")
            {
                response.Message = "CharacterEditorGameDbDir is not configured correctly on the daemon. Cannot export character data.";
                _logger.LogError(response.Message);
                return response;
            }
            if (!Directory.Exists(_characterEditorGameDbDir))
            {
                response.Message = $"Character Editor Game DB directory '{_characterEditorGameDbDir}' not found on server. Check daemon configuration 'PerfectWorldPaths:CharacterEditorGameDbDir'.";
                _logger.LogError(response.Message);
                return response;
            }

            string gamedbdExecutableName = "gamedbd";
            string gamedbdFullPath = Path.Combine(_characterEditorGameDbDir, gamedbdExecutableName);

            if (!File.Exists(gamedbdFullPath))
            {
                gamedbdFullPath = Path.Combine(_characterEditorGameDbDir, "./" + gamedbdExecutableName);
                if (!File.Exists(gamedbdFullPath))
                {
                    response.Message = $"gamedbd executable not found at '{Path.Combine(_characterEditorGameDbDir, gamedbdExecutableName)}' or as './{gamedbdExecutableName}' within that directory.";
                    _logger.LogError(response.Message);
                    return response;
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = gamedbdFullPath,
                Arguments = $"./gamesys.conf exportrole {request.CharacterId}",
                WorkingDirectory = _characterEditorGameDbDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _logger.LogInformation($"Executing for export: {psi.FileName} {psi.Arguments} in WD: {psi.WorkingDirectory}");

            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        response.Message = "Failed to start gamedbd process for export.";
                        _logger.LogError(response.Message);
                        return response;
                    }

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    bool exited = process.WaitForExit(15000);
                    if (!exited)
                    {
                        process.Kill(true);
                        response.Message = $"gamedbd export process timed out for Character ID {request.CharacterId}.";
                        _logger.LogError(response.Message);
                        return response;
                    }

                    if (process.ExitCode == 0)
                    {
                        response.XmlData = output.Trim();
                        response.Success = true;
                        response.Message = "Character data exported successfully.";
                        _logger.LogInformation($"Character {request.CharacterId} exported. XML Length: {output.Length}. ExitCode: {process.ExitCode}");
                    }
                    else
                    {
                        response.Message = $"gamedbd export failed. ExitCode: {process.ExitCode}. Error: {error?.Trim()}. Output: {output?.Trim()}";
                        _logger.LogError(response.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception during character export for ID {request.CharacterId}.");
                response.Message = $"Exception: {ex.Message}";
            }

            return response;
        }

        public override async Task<ImportCharacterResponse> ImportCharacterData(ImportCharacterRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new ImportCharacterResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to import character data for ID: {request.CharacterId} using GameDbDir: '{_characterEditorGameDbDir}' and PwAdminDir: '{_pwAdminDir}'");
            var response = new ImportCharacterResponse { Success = false };

            // ... (rest of the method remains the same)
            if (string.IsNullOrWhiteSpace(request.CharacterId))
            {
                response.Message = "Character ID cannot be empty for import.";
                _logger.LogWarning(response.Message);
                return response;
            }
            if (string.IsNullOrWhiteSpace(request.XmlData))
            {
                response.Message = "XML data cannot be empty for import.";
                _logger.LogWarning(response.Message);
                return response;
            }

            if (string.IsNullOrEmpty(_characterEditorGameDbDir) || _characterEditorGameDbDir == "/invalid_path_character_editor_gamedbdir_not_set")
            {
                response.Message = "CharacterEditorGameDbDir is not configured correctly on the daemon. Cannot import character data.";
                _logger.LogError(response.Message);
                return response;
            }
            if (!Directory.Exists(_characterEditorGameDbDir))
            {
                response.Message = $"Character Editor Game DB directory '{_characterEditorGameDbDir}' not found on server. Check daemon configuration.";
                _logger.LogError(response.Message);
                return response;
            }
            if (string.IsNullOrEmpty(_pwAdminDir) || !Directory.Exists(_pwAdminDir))
            {
                response.Message = $"PwAdminDir '{_pwAdminDir}' for updater script not found or not configured. Check daemon configuration.";
                _logger.LogError(response.Message);
                return response;
            }

            string tempXmlPath = Path.Combine(_characterEditorGameDbDir, "temp.xml");
            string updaterScriptName = "updater";
            string updaterScriptFullPath = Path.Combine(_pwAdminDir, updaterScriptName);

            if (!File.Exists(updaterScriptFullPath))
            {
                _logger.LogWarning($"Updater script '{updaterScriptFullPath}' not found. Trying 'updater.sh'.");
                updaterScriptName = "updater.sh";
                updaterScriptFullPath = Path.Combine(_pwAdminDir, updaterScriptName);
                if (!File.Exists(updaterScriptFullPath))
                {
                    _logger.LogWarning($"Updater script '{updaterScriptFullPath}' not found. Trying 'updater.bash'.");
                    updaterScriptName = "updater.bash";
                    updaterScriptFullPath = Path.Combine(_pwAdminDir, updaterScriptName);
                    if (!File.Exists(updaterScriptFullPath))
                    {
                        response.Message = $"Updater script not found. Looked for 'updater', 'updater.sh', 'updater.bash' in '{_pwAdminDir}'.";
                        _logger.LogError(response.Message);
                        return response;
                    }
                }
            }

            try
            {
                await File.WriteAllTextAsync(tempXmlPath, request.XmlData, Encoding.UTF8);
                _logger.LogInformation($"XML data for character {request.CharacterId} written to {tempXmlPath}");

                var psiUpdater = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{updaterScriptFullPath.Replace("\"", "\\\"")}\" \"{_characterEditorGameDbDir.Replace("\"", "\\\"")}\"",
                    WorkingDirectory = _pwAdminDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Executing updater script: {psiUpdater.FileName} {psiUpdater.Arguments} in WD: {psiUpdater.WorkingDirectory}");

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                int exitCode = -1;

                using (var process = new Process { StartInfo = psiUpdater })
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (process.WaitForExit(60000))
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        process.Kill(true);
                        _logger.LogError($"Updater script timed out for character {request.CharacterId}.");
                        response.Message = "Updater script timed out.";
                        if (File.Exists(tempXmlPath)) { try { File.Delete(tempXmlPath); } catch { } }
                        return response;
                    }
                }

                string updaterOutput = outputBuilder.ToString().TrimEnd();
                string updaterError = errorBuilder.ToString().TrimEnd();

                if (exitCode == 0)
                {
                    response.Success = true;
                    response.Message = $"Character data for ID {request.CharacterId} imported successfully. Updater output: {updaterOutput}";
                    _logger.LogInformation(response.Message);
                }
                else
                {
                    response.Message = $"Updater script failed for character {request.CharacterId}. ExitCode: {exitCode}. Error: {updaterError}. Output: {updaterOutput}";
                    _logger.LogError(response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception during character import for ID {request.CharacterId}.");
                response.Message = $"Exception: {ex.Message}";
            }
            finally
            {
                if (File.Exists(tempXmlPath))
                {
                    try { File.Delete(tempXmlPath); _logger.LogInformation($"Cleaned up {tempXmlPath}"); }
                    catch (Exception exDel) { _logger.LogWarning(exDel, $"Failed to cleanup {tempXmlPath}"); }
                }
            }
            return response;
        }

        public override async Task<GetPlayerCharactersResponse> GetPlayerCharacters(GetPlayerCharactersRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new GetPlayerCharactersResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"[ManagerService] GetPlayerCharacters RPC called for UserID: {request.UserId}. Using 'listrolebrief'.");
            var response = new GetPlayerCharactersResponse { Success = false };

            // ... (rest of the method remains the same)
            var foundCharacters = new List<PlayerCharacterItem>();

            string gamedbdBasePath = _characterEditorGameDbDir;
            string gamedbdExecutable = Path.Combine(gamedbdBasePath, "gamedbd");
            string arguments = $"./gamesys.conf listrolebrief";
            string commandForBash = $"cd '{gamedbdBasePath.Replace("'", "'\\''")}' && \"{gamedbdExecutable.Replace("\"", "\\\"")}\" {arguments}";

            _logger.LogInformation($"[ManagerService] Preparing to execute gamedbd command via bash: {commandForBash}");

            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{commandForBash.Replace("\"", "\\\"")}\"",
                WorkingDirectory = gamedbdBasePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            try
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                int exitCode = -1;

                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (process.WaitForExit(20000))
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        try { process.Kill(true); } catch (Exception killEx) { _logger.LogWarning(killEx, "Failed to kill gamedbd process on timeout for listrolebrief."); }
                        _logger.LogWarning($"[ManagerService] gamedbd 'listrolebrief' command timed out. UserID searched: {request.UserId}.");
                        response.Message = "Command to retrieve character list timed out.";
                        response.Success = false;
                        return response;
                    }
                }

                string stdOutput = outputBuilder.ToString();
                string stdError = errorBuilder.ToString();

                if (exitCode == 0)
                {
                    _logger.LogInformation($"[ManagerService] gamedbd 'listrolebrief' successful. Output lines: {stdOutput.Split('\n').Length}. Parsing for UserID: {request.UserId}.");
                    var lines = stdOutput.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length > 1)
                    {
                        for (int i = 1; i < lines.Length; i++)
                        {
                            string line = lines[i];
                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                string roleIdStr = parts[0].Trim();
                                string userIdStr = parts[1].Trim();
                                string nameStr = parts[2].Trim().Trim('"');
                                if (int.TryParse(userIdStr, out int lineUserId) && lineUserId == request.UserId)
                                {
                                    if (int.TryParse(roleIdStr, out int roleId))
                                    {
                                        foundCharacters.Add(new PlayerCharacterItem { RoleId = roleId, RoleName = nameStr });
                                        _logger.LogInformation($"[ManagerService] Parsed and matched: UserID={lineUserId}, RoleID={roleId}, Name='{nameStr}'");
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"[ManagerService] Failed to parse RoleID '{roleIdStr}' from line: {line}");
                                    }
                                }
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    _logger.LogWarning($"[ManagerService] Skipping malformed line (not enough parts after split by comma): \"{line}\"");
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"[ManagerService] 'listrolebrief' output contained no data lines after the header.");
                    }

                    if (foundCharacters.Any())
                    {
                        response.Characters.AddRange(foundCharacters);
                        response.Success = true;
                        response.Message = $"Successfully retrieved {foundCharacters.Count} characters for User ID {request.UserId}.";
                    }
                    else
                    {
                        response.Success = true; // Still success, just no data found
                        response.Message = $"No characters found for User ID {request.UserId} in 'listrolebrief' output after parsing.";
                        _logger.LogInformation($"[ManagerService] 'listrolebrief' output parsed, but no characters matched or found for UserID {request.UserId}. Full output sample if short: {stdOutput.Substring(0, Math.Min(stdOutput.Length, 1000))}");
                    }
                }
                else
                {
                    response.Message = $"gamedbd 'listrolebrief' command failed. ExitCode: {exitCode}.";
                    if (!string.IsNullOrWhiteSpace(stdError)) response.Message += $" Error: {stdError.Trim()}";
                    _logger.LogError($"[ManagerService] gamedbd 'listrolebrief' command failed. UserID searched: {request.UserId}. ExitCode: {exitCode}. Stderr: {stdError}. Stdout (first 1000 chars): {stdOutput.Substring(0, Math.Min(stdOutput.Length, 1000))}");
                    response.Success = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ManagerService] Exception executing 'listrolebrief' for UserID {request.UserId}.");
                response.Message = $"Server exception while executing command: {ex.Message}";
                response.Success = false;
            }

            _logger.LogInformation($"[ManagerService] GetPlayerCharacters RPC ('listrolebrief' method) completed for UserID: {request.UserId}. Success: {response.Success}, Message: {response.Message}, Characters sent to client: {response.Characters.Count}");
            return response;
        }

        // ----- Account Management Implementations -----

        public override async Task<AccountActionResponse> CreateAccount(CreateAccountRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new AccountActionResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to create account: {request.Username}");
            try
            {
                string result = await _dbService.CreateAccountAsync(request.Username, request.Password, request.Email);
                bool success = result.Contains("successfully"); // Fragile check, but matches DatabaseService's current output
                _logger.LogInformation($"Account creation for {request.Username} result: {result}");
                return new AccountActionResponse { Success = success, Message = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating account {request.Username}");
                return new AccountActionResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }

        public override async Task<AccountActionResponse> ChangePassword(ChangePasswordRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new AccountActionResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to change password for user: {request.Username}");
            try
            {
                string result = await _dbService.ChangePasswordAsync(request.Username, request.OldPassword, request.NewPassword);
                bool success = result.Contains("successfully");
                _logger.LogInformation($"Password change for {request.Username} result: {result}");
                return new AccountActionResponse { Success = success, Message = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for {request.Username}");
                return new AccountActionResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }

        public override async Task<AccountActionResponse> AddCubi(AddCubiRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new AccountActionResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to add cubi for: {request.Identifier} (IsById: {request.IsById}), Amount: {request.Amount}");
            try
            {
                string result = await _dbService.AddCubiAsync(request.Identifier, request.IsById, request.Amount);
                bool success = result.Contains("successfully");
                _logger.LogInformation($"Add cubi for {request.Identifier} result: {result}");
                return new AccountActionResponse { Success = success, Message = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding cubi for {request.Identifier}");
                return new AccountActionResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }

        public override async Task<GetAllUsersResponse> GetAllUsers(GetAllUsersRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new GetAllUsersResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation("Attempting to get all users.");
            try
            {
                var userCoreInfos = await _dbService.GetAllUsersAsync();
                var userMessages = userCoreInfos.Select(u => new UserAccountInfoMessage
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    CreateTime = u.CreateTime.ToString("o"), // ISO 8601 format
                    IsGm = u.IsGm
                }).ToList();

                _logger.LogInformation($"Retrieved {userMessages.Count} users.");
                return new GetAllUsersResponse { Users = { userMessages }, Success = true, Message = $"Retrieved {userMessages.Count} users." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users.");
                return new GetAllUsersResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }

        public override async Task<AccountActionResponse> SetGmStatus(SetGmStatusRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new AccountActionResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to set GM status for: {request.Identifier} (IsById: {request.IsById}), Grant: {request.GrantAccess}");
            try
            {
                string result = await _dbService.SetGmStatusAsync(request.Identifier, request.IsById, request.GrantAccess);
                // Assuming result indicates success if it doesn't start with "Error" or "Failed"
                bool success = !result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) &&
                               !result.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
                               !result.Contains("doesn't exist");
                _logger.LogInformation($"Set GM status for {request.Identifier} result: {result}");
                return new AccountActionResponse { Success = success, Message = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting GM status for {request.Identifier}");
                return new AccountActionResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }

        public override async Task<AccountActionResponse> DeleteUser(DeleteUserRequest request, ServerCallContext context)
        {
            if (!IsApiKeyValid(context)) return new AccountActionResponse { Success = false, Message = context.Status.Detail };
            _logger.LogInformation($"Attempting to delete user: {request.Identifier} (IsById: {request.IsById})");
            try
            {
                string result = await _dbService.DeleteUserAsync(request.Identifier, request.IsById);
                bool success = result.Contains("deleted");
                _logger.LogInformation($"Delete user {request.Identifier} result: {result}");
                return new AccountActionResponse { Success = success, Message = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user {request.Identifier}");
                return new AccountActionResponse { Success = false, Message = $"Server exception: {ex.Message}" };
            }
        }
    }
}