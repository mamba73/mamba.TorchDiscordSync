using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Core;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Plugin
{
    /// <summary>
    /// mamba.TorchDiscordSync - Advanced Space Engineers faction/Discord sync plugin
    /// with death logging, chat sync, server monitoring, and admin commands.
    /// </summary>
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        // Core services
        private DatabaseService _db;
        private FactionSyncService _factionSync;
        private DiscordBotService _discordBot;
        private DiscordService _discordWrapper;
        private EventLoggingService _eventLog;
        private ChatSyncService _chatSync;
        private DeathLogService _deathLog;
        private VerificationService _verification;
        private VerificationCommandHandler _verificationCommandHandler;
        private SyncOrchestrator _orchestrator;

        // Configurations
        private MainConfig _config;
        private DiscordBotConfig _discordBotConfig;

        // Timers and state
        private Timer _syncTimer;
        private ITorchSession _currentSession;
        private bool _isInitialized = false;
        private bool _serverStartupLogged = false;

        /// <summary>
        /// Plugin initialization - called when Torch loads the plugin
        /// </summary>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            try
            {
                PrintBanner("INITIALIZING");

                // Load configurations
                _config = MainConfig.Load();
                if (_config != null)
                {
                    LoggerUtil.LogInfo("Configuration loaded - Debug mode: " + _config.Debug);
                }
                else 
                {
                    LoggerUtil.LogError("Failed to load configuration!");
                    return;
                }

                // Build a compatibility DiscordBotConfig from MainConfig.Discord for existing services
                _discordBotConfig = new DiscordBotConfig();
                if (_config != null && _config.Discord != null)
                {
                    _discordBotConfig.BotToken = _config.Discord.BotToken;
                    _discordBotConfig.GuildID = _config.Discord.GuildID;
                    _discordBotConfig.BotPrefix = _config.Discord.BotPrefix;
                    _discordBotConfig.EnableDMNotifications = _config.Discord.EnableDMNotifications;
                    _discordBotConfig.VerificationCodeExpirationMinutes = _config.Discord.VerificationCodeExpirationMinutes;
                }

                // Initialize database service (XML-based)
                _db = new DatabaseService();
                LoggerUtil.LogSuccess("Database service initialized (XML-based)");

                // Initialize Discord bot service
                _discordBot = new DiscordBotService(_discordBotConfig);
                Task.Run(delegate
                {
                    return ConnectBotAsync();
                });

                // Initialize Discord wrapper
                _discordWrapper = new DiscordService(_discordBot);

                // Initialize verification service
                _verification = new VerificationService(_db);

                // Initialize event logging service
                _eventLog = new EventLoggingService(_db, _discordWrapper, _config);

                // Initialize faction sync service
                _factionSync = new FactionSyncService(_db, _discordWrapper);

                // Initialize chat sync service
                _chatSync = new ChatSyncService(_discordWrapper, _config, _db);

                // Initialize death log service
                _deathLog = new DeathLogService(_db, _eventLog);

                // Initialize verification command handler
                _verificationCommandHandler = new VerificationCommandHandler(
                    _verification, _eventLog, _config, _discordBot, _discordBotConfig);

                // Initialize sync orchestrator
                _orchestrator = new SyncOrchestrator(_db, _discordWrapper, _factionSync, _eventLog, _config);

                LoggerUtil.LogSuccess("All services initialized");

                // Send 'Server Started' message if enabled in config
                // if (_config != null && _config.Enabled && !string.IsNullOrEmpty(_config.ServerStartedMessage))
                // {
                    // Assuming _discordBot is your wrapper class that has SendMessage
                    // _discordBot?.SendMessage(_config.ServerStartedMessage);
                // }

                // Hook Discord bot verification event
                if (_discordBot != null)
                {
                    _discordBot.OnVerificationAttempt += delegate (string code, ulong discordID, string discordUsername)
                    {
                        Task.Run(delegate
                        {
                            return HandleVerificationAsync(code, discordID, discordUsername);
                        });
                    };
                }

                // Register session event handler
                var sessionManager = torch.Managers.GetManager<ITorchSessionManager>();
                if (sessionManager != null)
                {
                    sessionManager.SessionStateChanged += OnSessionStateChanged;
                    LoggerUtil.LogSuccess("Session manager hooked");
                }
                else
                {
                    LoggerUtil.LogError("Session manager not available!");
                }

                // Initialize sync timer
                int syncInterval = 5000;
                if (_config != null)
                {
                    syncInterval = _config.SyncIntervalSeconds * 1000;
                }
                _syncTimer = new Timer(syncInterval);
                _syncTimer.Elapsed += OnSyncTimerElapsed;
                _syncTimer.AutoReset = true;

                _isInitialized = true;
                PrintBanner("INITIALIZATION COMPLETE");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Plugin initialization failed: " + ex.Message);
                LoggerUtil.LogError("Stack trace: " + ex.StackTrace);
                _isInitialized = false;
            }
        }

        private Task ConnectBotAsync()
        {
            if (_discordBot != null)
            {
                return _discordBot.ConnectAsync().ContinueWith(t =>
                {
                    if (t.Result)
                    {
                        LoggerUtil.LogSuccess("Discord Bot connected and ready");
                    }
                });
            }
            return Task.FromResult(0);
        }

        private Task HandleVerificationAsync(string code, ulong discordID, string discordUsername)
        {
            if (_verificationCommandHandler != null)
            {
                return _verificationCommandHandler.VerifyFromDiscordAsync(code, discordID, discordUsername).ContinueWith(t =>
                {
                    LoggerUtil.LogInfo("[VERIFY] Verification result: " + t.Result);
                });
            }
            return Task.FromResult(0);
        }

        private void PrintBanner(string title)
        {
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════╗");
            Console.WriteLine("║ " + VersionUtil.GetPluginName() + " " + VersionUtil.GetVersionString() + " - " + title.PadRight(20) + "║");
            Console.WriteLine("╚════════════════════════════════════════════════════╝");
            Console.WriteLine("");
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            _currentSession = session;

            switch (state)
            {
                case TorchSessionState.Loading:
                    LoggerUtil.LogInfo("═══ Server session LOADING ═══");
                    _serverStartupLogged = false;
                    break;

                case TorchSessionState.Loaded:
                    LoggerUtil.LogSuccess("═══ Server session LOADED ═══");
                    _serverStartupLogged = false;

                    // Run startup routines
                    if (_isInitialized)
                    {
                        Task.Run(async () => await OnServerLoadedAsync(session));
                    }
                    break;

                case TorchSessionState.Unloading:
                    LoggerUtil.LogInfo("═══ Server session UNLOADING ═══");
                    if (_syncTimer != null && _syncTimer.Enabled)
                        _syncTimer.Stop();
                    break;

                case TorchSessionState.Unloaded:
                    LoggerUtil.LogWarning("═══ Server session UNLOADED ═══");
                    break;
            }
        }

        /// <summary>
        /// Runs after server is fully loaded
        /// </summary>
        private async Task OnServerLoadedAsync(ITorchSession session)
        {
            try
            {
                if (_serverStartupLogged)
                    return; // Prevent duplicate logging

                _serverStartupLogged = true;

                LoggerUtil.LogInfo("[STARTUP] Initializing server sync...");

                // Get current SimSpeed
                float currentSimSpeed = 1.0f; // Placeholder
                if (session != null)
                {
                    try
                    {
                        // TODO: Get real SimSpeed from session
                        // currentSimSpeed = GetSimSpeedFromSession(session);
                    }
                    catch { }
                }

                // Check server status
                await _orchestrator.CheckServerStatusAsync(currentSimSpeed);

                // Load factions from save
                var factions = LoadFactionsFromSession(session);
                if (factions.Count > 0)
                {
                    LoggerUtil.LogInfo("[STARTUP] Found " + factions.Count + " player factions");
                    await _orchestrator.ExecuteFullSyncAsync(factions);
                }
                else
                {
                    LoggerUtil.LogWarning("[STARTUP] No player factions found (tag length != 3)");
                }

                // Start periodic sync timer
                if (_syncTimer != null && !_syncTimer.Enabled)
                {
                    _syncTimer.Start();
                    LoggerUtil.LogSuccess("[STARTUP] Sync timer started (interval: " + _config.SyncIntervalSeconds + "s)");
                }

                LoggerUtil.LogSuccess("[STARTUP] Server startup sync complete!");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[STARTUP] Error: " + ex.Message);
                await _eventLog.LogAsync("StartupError", ex.Message);
            }
        }

        private void OnSyncTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_orchestrator != null)
            {
                _orchestrator.SyncFactionsAsync().Wait();
            }
        }

        /// <summary>
        /// Handles /tds commands from in-game chat
        /// Validates authorization before executing commands
        /// </summary>
        public void HandleChatCommand(string command, long playerSteamID, string playerName)
        {
            try
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    HandleHelpCommand(playerSteamID);
                    return;
                }

                var subcommand = parts[1].ToLower();

                // Validate command and authorization
                CommandModel cmdModel = CommandAuthorizationUtil.ParseCommand(subcommand, playerSteamID, _config.AdminSteamIDs);

                if (cmdModel == null)
                {
                    // Command doesn't exist OR user is not authorized
                    bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(playerSteamID, _config.AdminSteamIDs);
                    var allCmds = CommandAuthorizationUtil.GetAllCommands();
                    var fullCmd = null as CommandModel;
                    for (int i = 0; i < allCmds.Count; i++)
                    {
                        if (allCmds[i].Name.Equals(subcommand, StringComparison.OrdinalIgnoreCase))
                        {
                            fullCmd = allCmds[i];
                            break;
                        }
                    }

                    if (fullCmd != null && fullCmd.RequiresAdmin && !isAdmin)
                    {
                        // Command exists but user is not authorized
                        LoggerUtil.LogWarning("[SECURITY] Unauthorized command attempt by " + playerName + " (" + playerSteamID + "): /" + subcommand);
                        ChatUtils.SendError("Access denied. Command '" + subcommand + "' requires admin privileges.");
                        return;
                    }
                    else
                    {
                        // Command doesn't exist
                        ChatUtils.SendError("Unknown command: /tds " + subcommand + ". Type /tds help for available commands.");
                        return;
                    }
                }

                // Execute authorized command
                switch (subcommand)
                {
                    case "verify":
                        HandleVerifyCommand(playerSteamID, playerName, parts);
                        break;

                    case "status":
                        Task.Run(async () => await HandleStatusCommand(playerName));
                        break;

                    case "sync":
                        Task.Run(async () => await HandleSyncCommand(playerName));
                        break;

                    case "reset":
                        Task.Run(async () => await HandleResetCommand(playerName));
                        break;

                    case "unverify":
                        HandleUnverifyCommand(playerSteamID, playerName, parts);
                        break;

                    case "help":
                        HandleHelpCommand(playerSteamID);
                        break;

                    default:
                        ChatUtils.SendError("Unknown command: /tds " + subcommand);
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[COMMAND] Error: " + ex.Message);
                ChatUtils.SendError("Command error: " + ex.Message);
            }
        }

        /// <summary>
        /// Display help text based on user authorization level
        /// Admins see all commands, users see only public commands
        /// </summary>
        private void HandleHelpCommand(long playerSteamID)
        {
            try
            {
                string helpText = CommandAuthorizationUtil.GenerateHelpText(playerSteamID, _config);

                // Split into lines and send each
                var lines = helpText.Split('\n');
                foreach (var line in lines)
                {
                    ChatUtils.SendServerMessage(line);
                }

                bool isAdmin = SecurityUtil.IsPlayerAdmin(playerSteamID, _config.AdminSteamIDs);
                LoggerUtil.LogInfo("[COMMAND] " + (isAdmin ? "ADMIN" : "USER") + " help displayed");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[HELP] Error: " + ex.Message);
                ChatUtils.SendError("Error displaying help");
            }
        }

        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// Available to all users
        /// </summary>
        private void HandleVerifyCommand(long playerSteamID, string playerName, string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds verify @DiscordName");
                    return;
                }

                string discordUsername = args[2];

                Task.Run(async () =>
                {
                    var result = await _verificationCommandHandler.HandleVerifyCommandAsync(
                        playerSteamID, playerName, discordUsername);
                    ChatUtils.SendServerMessage(result);
                });

                LoggerUtil.LogInfo("[COMMAND] " + playerName + ": verify requested for " + discordUsername);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Verification error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds sync command
        /// Available only to admins
        /// </summary>
        private async Task HandleSyncCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds sync");
                ChatUtils.SendSuccess("Starting faction synchronization...");
                await _eventLog.LogAsync("Command", "Manual sync by " + playerName);

                if (_currentSession != null)
                {
                    var factions = LoadFactionsFromSession(_currentSession);
                    await _orchestrator.ExecuteFullSyncAsync(factions);
                    ChatUtils.SendSuccess("Synchronization complete!");
                }
                else
                {
                    ChatUtils.SendError("No active session found");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[SYNC_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Sync error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds reset command
        /// Available only to admins
        /// WARNING: This is destructive
        /// </summary>
        private async Task HandleResetCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogWarning("[COMMAND] " + playerName + " executed: /tds reset (DESTRUCTIVE)");
                ChatUtils.SendWarning("Clearing Discord roles and channels...");

                await _factionSync.ResetDiscordAsync();
                await _eventLog.LogAsync("Command", "Discord reset executed by " + playerName);

                ChatUtils.SendSuccess("Discord reset complete! User roles updated.");
                LoggerUtil.LogSuccess("[RESET] Completed by " + playerName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[RESET_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Reset error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds unverify STEAMID [reason] command
        /// Available only to admins
        /// </summary>
        private void HandleUnverifyCommand(long adminSteamID, string adminName, string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds unverify STEAMID [reason]");
                    return;
                }

                long targetSteamID = 0;
                if (!long.TryParse(args[2], out targetSteamID))
                {
                    ChatUtils.SendError("Invalid Steam ID format");
                    return;
                }

                string reason = "Admin removal";
                if (args.Length > 3)
                {
                    var reasonParts = new List<string>();
                    for (int i = 3; i < args.Length; i++)
                    {
                        reasonParts.Add(args[i]);
                    }
                    reason = string.Join(" ", reasonParts);
                }

                Task.Run(async () =>
                {
                    var result = await _verificationCommandHandler.HandleUnverifyCommandAsync(targetSteamID, reason);
                    ChatUtils.SendServerMessage(result);
                    await _eventLog.LogAsync("UnverifyCommand",
                        adminName + " unverified SteamID " + targetSteamID + ": " + reason);
                });

                LoggerUtil.LogInfo("[COMMAND] " + adminName + " executed: /tds unverify " + targetSteamID + " (" + reason + ")");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[UNVERIFY_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Unverify error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds status command
        /// Available to all users
        /// </summary>
        private async Task HandleStatusCommand(string playerName)
        {
            try
            {
                var factions = _db.GetAllFactions();
                int totalPlayers = 0;
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].Players != null)
                        totalPlayers += factions[i].Players.Count;
                }

                // Get verified count from VerificationService
                var verifications = _db.GetAllVerifications();
                int verifiedCount = 0;
                if (verifications != null)
                {
                    for (int i = 0; i < verifications.Count; i++)
                    {
                        if (verifications[i] != null && verifications[i].IsVerified)
                            verifiedCount++;
                    }
                }

                var statusLines = new List<string>();
                statusLines.Add("");
                statusLines.Add("- Plugin Status -");
                statusLines.Add("Factions: " + factions.Count);
                statusLines.Add("Players: " + totalPlayers);
                statusLines.Add("Verified Accounts: " + verifiedCount);
                statusLines.Add("Debug Mode: " + (_config.Debug ? "ON" : "OFF"));
                statusLines.Add("");

                foreach (var line in statusLines)
                {
                    ChatUtils.SendServerMessage(line);
                }

                await _eventLog.LogAsync("StatusCommand", "Status requested by " + playerName);
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds status");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[STATUS_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Status error: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads factions from Space Engineers save
        /// TODO: Integrate with real Space Engineers API
        /// </summary>
        private List<FactionModel> LoadFactionsFromSession(ITorchSession session)
        {
            var factions = new List<FactionModel>();

            try
            {
                if (session == null)
                    return factions;

                // TODO: Replace with real data loading
                // For now, just create a dummy faction for testing
                var testFaction = new FactionModel();
                testFaction.Tag = "ABC";
                testFaction.Name = "Test Faction";
                testFaction.Players = new List<FactionPlayerModel>
                {
                    new FactionPlayerModel { SteamID = 123456789, DiscordUserID = 987654321 },
                    new FactionPlayerModel { SteamID = 234567890, DiscordUserID = 876543210 }
                };

                factions.Add(testFaction);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error loading factions from session: " + ex.Message);
            }

            return factions;
        }
    }
}