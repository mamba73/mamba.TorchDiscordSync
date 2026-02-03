// Plugin/MambaTorchDiscordSyncPlugin.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Discord.WebSocket;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Core;
using mamba.TorchDiscordSync.Handlers;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;
using Sandbox;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.Game; // <-- Add this for MyVisualScriptLogicProvider
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Managers.ChatManager;

namespace mamba.TorchDiscordSync.Plugin
{
    /// <summary>
    /// mamba.TorchDiscordSync - Advanced Space Engineers faction/Discord sync plugin
    /// with death logging, chat sync, server monitoring, and admin commands.
    /// NEW: Integrated with Death Location Zones system.
    /// </summary>
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        // Core services
        private DatabaseService _db;
        private FactionSyncService _factionSync;
        private DiscordBotService _discordBot;
        private DiscordService _discordWrapper;
        private EventLoggingService _eventLog;
        private ITorchBase _torch;
        private ChatSyncService _chatSync;
        private DeathLogService _deathLog;
        private VerificationService _verification;
        private VerificationCommandHandler _verificationCommandHandler;
        private SyncOrchestrator _orchestrator;
        private PlayerTrackingService _playerTracking;
        private FactionReaderService _factionReader;

        // Handlers
        private CommandProcessor _commandProcessor;
        private EventManager _eventManager;
        private ChatModerator _chatModerator;

        // Configurations
        private MainConfig _config;
        private DiscordBotConfig _discordBotConfig;

        // Public accessor for configuration (read-only)
        public MainConfig Config
        {
            get { return _config; }
        }

        // Timers and state
        private Timer _syncTimer;
        private ITorchSession _currentSession;
        private bool _isInitialized = false;
        private bool _serverStartupLogged = false;
        private bool _playerTrackingInitialized = false;

        /// <summary>
        /// Plugin initialization - called when Torch loads the plugin
        /// </summary>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            try
            {
                PrintBanner("INITIALIZING");

                base.Init(torch);
                _torch = torch;

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
                    _discordBotConfig.VerificationCodeExpirationMinutes = _config
                        .Discord
                        .VerificationCodeExpirationMinutes;
                }

                // Initialize database service (XML-based)
                _db = new DatabaseService();
                LoggerUtil.LogSuccess("Database service initialized (XML-based)");

                // Initialize Discord bot service
                _discordBot = new DiscordBotService(_discordBotConfig);
                Task.Run(
                    delegate
                    {
                        return ConnectBotAsync();
                    }
                );

                // Initialize Discord wrapper
                _discordWrapper = new DiscordService(_discordBot);

                // Initialize verification service
                _verification = new VerificationService(_db);

                // Initialize event logging service
                _eventLog = new EventLoggingService(_db, _discordWrapper, _config);

                // Initialize death log service FIRST so we can pass it to PlayerTrackingService
                // NEW: Pass MainConfig to DeathLogService for location zones configuration
                _deathLog = new DeathLogService(_db, _eventLog, _config);

                _factionReader = new FactionReaderService();

                _factionSync = new FactionSyncService(
                    _db,
                    _discordWrapper,
                    _config,
                    // _factionReader
                    _factionReader
                );

                // Initialize faction reader service for loading real faction data
                _factionReader = new FactionReaderService();

                // CRITICAL FIX: Safe cast to TorchBase for PlayerTrackingService
                var torchBase = torch as Torch.TorchBase;
                if (torchBase == null)
                {
                    LoggerUtil.LogError(
                        "Torch instance is not TorchBase! Compatibility with this Torch version is not guaranteed."
                    );
                    _playerTracking = null;
                    return;
                }

                // Initialize player tracking service - DO NOT call Initialize() yet
                // It will be called in OnSessionStateChanged when session is loaded
                _playerTracking = new PlayerTrackingService(_eventLog, torchBase, _deathLog);

                // Initialize chat sync service
                _chatSync = new ChatSyncService(_discordWrapper, _config, _db);

                // Hook Discord messages for chat sync using public event
                if (_discordBot != null)
                {
                    _discordBot.OnMessageReceivedEvent += async msg =>
                    {
                        // Only process messages from the configured chat channel
                        if (
                            msg.Channel is SocketTextChannel textChannel
                            && textChannel.Id == _config.Discord.ChatChannelId
                        )
                        {
                            // Skip bot messages and commands (prevent loops)
                            if (
                                msg.Author.IsBot
                                || msg.Content.StartsWith(_config.Discord.BotPrefix)
                            )
                                return;

                            // Forward to ChatSyncService to send to game chat
                            await _chatSync.SendDiscordMessageToGameAsync(
                                msg.Author.Username,
                                msg.Content
                            );
                            LoggerUtil.LogDebug(
                                $"[DISCORD›GAME] Forwarded message from {msg.Author.Username}"
                            );
                        }
                    };
                }

                // Initialize sync orchestrator
                _orchestrator = new SyncOrchestrator(
                    _db,
                    _discordWrapper,
                    _factionSync,
                    _eventLog,
                    _config
                );

                // Initialize handlers
                _commandProcessor = new CommandProcessor(
                    _config,
                    _discordWrapper,
                    _db,
                    _factionSync,
                    _eventLog,
                    _orchestrator
                );
                LoggerUtil.LogDebug("[INIT] CommandProcessor initialized");
                _eventManager = new EventManager(_config, _discordWrapper, _eventLog);
                _chatModerator = new ChatModerator(_config, _discordWrapper, _db);

                // Initialize verification command handler
                _verificationCommandHandler = new VerificationCommandHandler(
                    _verification,
                    _eventLog,
                    _config,
                    _discordBot,
                    _discordBotConfig
                );

                LoggerUtil.LogSuccess("All services initialized");

                // Hook Discord bot verification event
                if (_discordBot != null)
                {
                    _discordBot.OnVerificationAttempt += delegate(
                        string code,
                        ulong discordID,
                        string discordUsername
                    )
                    {
                        Task.Run(
                            delegate
                            {
                                return HandleVerificationAsync(code, discordID, discordUsername);
                            }
                        );
                    };
                }

                // Register session event handler
                var sessionManagerObj = torch.Managers.GetManager(typeof(ITorchSessionManager));
                var sessionManager = sessionManagerObj as ITorchSessionManager;
                if (sessionManager != null)
                {
                    sessionManager.SessionStateChanged += OnSessionStateChanged;
                    LoggerUtil.LogSuccess("Session manager hooked");
                }
                else
                {
                    LoggerUtil.LogError(
                        "Session manager not available! Check Torch version or references."
                    );
                }

                // NOTE: PlayerTrackingService.Initialize() will be called in OnSessionStateChanged(Loaded)
                // This ensures ChatManagerServer and DamageSystem are available
                LoggerUtil.LogInfo("Player tracking will initialize when session loads");

                // Initialize sync timer (only if faction sync is enabled)
                _syncTimer = PluginUtils.CreateSyncTimerIfEnabled(
                    _config,
                    OnSyncTimerElapsed
                );

                _isInitialized = true;
                PrintBanner("INITIALIZATION COMPLETE");

                // Save config after load (ensures any merged/default values are persisted)
                if (_config != null)
                {
                    _config.Save();
                    LoggerUtil.LogInfo("Configuration saved after initialization/load");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Plugin initialization failed: " + ex.Message);
                LoggerUtil.LogError("Stack trace: " + ex.StackTrace);
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Hooks Torch chat system for command processing (/tds commands)
        /// </summary>
        private void HookChatCommands(ITorchBase torch)
        {
            try
            {
                LoggerUtil.LogInfo("Hooking Torch chat message handler for command processing");
                var torchInstance = _torch as ITorchServer;
                var chatManager =
                    torchInstance?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
                if (chatManager != null)
                {
                    chatManager.MessageRecieved += OnChatMessageProcessing;
                    LoggerUtil.LogInfo("Registered Torch chat message handler");
                }
                else
                {
                    LoggerUtil.LogWarning("ChatManagerServer is null - chat integration disabled");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error hooking chat commands: {ex.Message}");
            }
        }

        /// <summary>
        /// CRITICAL: Primary chat message handler - processes ALL chat messages
        /// Priority order:
        /// 1. Detect and handle /tds commands (with proper SteamID)
        /// 2. Filter out faction chat (security - don't leak to Discord)
        /// 3. Forward global chat to Discord sync
        /// </summary>
        private void OnChatMessageProcessing(TorchChatMessage msg, ref bool consumed)
        {
            try
            {
                if (string.IsNullOrEmpty(msg.Message))
                    return;

                string channelName = msg.Channel.ToString() ?? "Unknown";

                LoggerUtil.LogDebug($"[CHAT] Author: {msg.Author}, Channel: {channelName}, Message: {msg.Message}");

                // PRIORITY 1: Check for /tds commands FIRST (before any filtering)
                // This ensures commands work in ALL channels (Global and Faction)
                if (msg.Message.StartsWith("/tds ") || msg.Message.Equals("/tds"))
                {
                    if (msg.AuthorSteamId.HasValue)
                    {
                        LoggerUtil.LogInfo($"[COMMAND] Detected: {msg.Message} from {msg.Author}");
                        HandleChatCommand(msg.Message, (long)msg.AuthorSteamId.Value, msg.Author);
                        consumed = true; // Prevent further processing
                    }
                    else
                    {
                        LoggerUtil.LogWarning($"[COMMAND] No SteamID for command: {msg.Message}");
                    }
                    return; // CRITICAL: Stop processing - don't forward commands to Discord
                }

                // PRIORITY 2: Filter out private /w (security - don't leak to Discord)
                if (channelName.StartsWith("Private") || channelName == "Private")
                {
                    LoggerUtil.LogDebug("[CHAT] Skipping private chat (not forwarded to Discord)");
                    return; // Stop processing - private chat stays private
                }
                // PRIORITY 3: Filter out faction chat (security - don't leak to Discord)
                if (channelName.StartsWith("Faction") || channelName == "Faction")
                {
                    LoggerUtil.LogDebug("[CHAT] Skipping faction chat (not forwarded to Discord)");
                    return; // Stop processing - faction chat stays private
                }

                // PRIORITY 4: Prevent Discord loop messages
                if (
                    msg.Message.StartsWith("[Discord] ")
                    || msg.Message.StartsWith("Discord")
                    || msg.Message.StartsWith("Server:")
                )
                {
                    LoggerUtil.LogDebug($"[CHAT] Skipped Discord loop: Author: {msg.Author}, Message: {msg.Message}");
                    return; // Prevent Discord loop messages
                }

                // PRIORITY 5: Prevent duplication of Server event messages
                if (msg.Author == "Server")
                {
                    LoggerUtil.LogDebug($"[CHAT] Skipped Server message to prevent duplication on Discord: Author: {msg.Author}, Message: {msg.Message}");
                    return;
                }

                // PRIORITY 6: Forward ONLY global chat to Discord sync
                if (channelName == "Global" || channelName.StartsWith("Global") )
                {
                    LoggerUtil.LogDebug($"[CHAT] Forwarding global chat to Discord: Author: {msg.Author}, Message: {msg.Message}");
                    ProcessChatMessage(msg.Message, msg.Author, "Global");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in chat message processing: {ex.Message}");
            }
        }

        private Task ConnectBotAsync()
        {
            if (_discordBot != null)
            {
                return _discordBot
                    .ConnectAsync()
                    .ContinueWith(t =>
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
                return _verificationCommandHandler
                    .VerifyFromDiscordAsync(code, discordID, discordUsername)
                    .ContinueWith(t =>
                    {
                        LoggerUtil.LogInfo("[VERIFY] Verification result: " + t.Result);
                    });
            }
            return Task.FromResult(0);
        }

        private void PrintBanner(string title)
        {
            Console.WriteLine("");
            Console.WriteLine("-====================================================¬");
            Console.WriteLine(
                "¦ "
                    + VersionUtil.GetPluginName()
                    + " "
                    + VersionUtil.GetVersionString()
                    + " - "
                    + title.PadRight(20)
                    + "¦"
            );
            Console.WriteLine("L====================================================-");
            Console.WriteLine("");
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            _currentSession = session;
            switch (state)
            {
                case TorchSessionState.Loading:
                    LoggerUtil.LogInfo("=== Server session LOADING ===");
                    _serverStartupLogged = false;
                    break;

                case TorchSessionState.Loaded:
                    LoggerUtil.LogSuccess("=== Server session LOADED ===");
                    _serverStartupLogged = false;

                    // Initialize PlayerTrackingService NOW when session is loaded
                    // This ensures ChatManagerServer and DamageSystem are available
                    if (_playerTracking != null && !_playerTrackingInitialized)
                    {
                        try
                        {
                            _playerTracking.Initialize();
                            _playerTrackingInitialized = true;
                            LoggerUtil.LogSuccess(
                                "Player tracking service initialized after session load"
                            );
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError(
                                $"Failed to initialize player tracking: {ex.Message}"
                            );
                        }
                    }

                    // NEW: Hook chat commands NOW when session is loaded
                    try
                    {
                        var torchInstance = _torch as ITorchServer;
                        if (
                            torchInstance != null
                            && torchInstance.CurrentSession != null
                            && torchInstance.CurrentSession.Managers != null
                        )
                        {
                            var chatManager =
                                torchInstance.CurrentSession.Managers.GetManager<ChatManagerServer>();
                            if (chatManager != null)
                            {
                                chatManager.MessageRecieved += OnChatMessageProcessing;
                                LoggerUtil.LogSuccess(
                                    "Torch chat message handler registered for /tds commands"
                                );
                            }
                            else
                            {
                                LoggerUtil.LogWarning(
                                    "ChatManagerServer is still null after session loaded - chat commands disabled"
                                );
                            }
                        }
                        else
                        {
                            LoggerUtil.LogWarning(
                                "CurrentSession or Managers is null after session loaded - chat commands disabled"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            "Failed to hook chat commands after session load: " + ex.Message
                        );
                    }

                    // Get actual simulation speed
                    float currentSimSpeed = PluginUtils.GetCurrentSimSpeed();

                    // Send server startup message with real SimSpeed
                    if (_eventLog != null && _config != null && _config.Monitoring != null)
                    {
                        bool monitoringEnabled = _config?.Monitoring?.Enabled == true;
                        if (monitoringEnabled)
                        {
                            Task.Run(async () =>
                            {
                                await _eventLog.LogServerStatusAsync("STARTED", currentSimSpeed);
                            });
                        }
                    }

                    // Run startup routines
                    if (_isInitialized)
                    {
                        OnServerLoadedAsync(session);
                    }
                    break;

                case TorchSessionState.Unloading:
                    LoggerUtil.LogInfo("=== Server session UNLOADING ===");
                    if (_syncTimer != null && _syncTimer.Enabled)
                        _syncTimer.Stop();
                    break;

                case TorchSessionState.Unloaded:
                    LoggerUtil.LogWarning("=== Server session UNLOADED ===");
                    _playerTrackingInitialized = false;

                    // Send server shutdown message
                    if (_eventLog != null && _config != null && _config.Monitoring != null)
                    {
                        bool monitoringEnabled = _config?.Monitoring?.Enabled == true;
                        if (monitoringEnabled)
                        {
                            Task.Run(async () =>
                            {
                                await _eventLog.LogServerStatusAsync("STOPPED", 0);
                            });
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Runs after server is fully loaded
        /// </summary>
        private void OnServerLoadedAsync(ITorchSession session)
        {
            try
            {
                if (_serverStartupLogged)
                    return;
                _serverStartupLogged = true;
                LoggerUtil.LogInfo("[STARTUP] Initializing server sync...");

                // Get current SimSpeed from session
                float currentSimSpeed = PluginUtils.GetCurrentSimSpeed();

                // Check server status
                _orchestrator.CheckServerStatusAsync(currentSimSpeed).Wait();

                // Load real factions from game using FactionReaderService
                var factions = _factionSync.LoadFactionsFromGame();
                if (factions.Count > 0)
                {
                    LoggerUtil.LogInfo("[STARTUP] Found " + factions.Count + " player factions");
                    // disabled
                    // _orchestrator.ExecuteFullSyncAsync(factions).Wait();
                }
                else
                {
                    LoggerUtil.LogWarning("[STARTUP] No player factions found (tag length != 3)");
                }

                // Start periodic sync timer (only if enabled and timer was created)
                PluginUtils.StartSyncTimerIfEnabled(_syncTimer, _config);

                LoggerUtil.LogSuccess("[STARTUP] Server startup sync complete!");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[STARTUP] Error: " + ex.Message);
                if (_eventLog != null)
                {
                    _eventLog.LogAsync("StartupError", ex.Message).Wait();
                }
            }
        }

        /// <summary>
        /// Periodic faction sync timer handler
        /// Only runs if faction sync is enabled in config
        /// </summary>
        private void OnSyncTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Double-check that faction sync is enabled before running
            if (PluginUtils.IsFactionSyncEnabled(_config))
            {
                if (_orchestrator != null)
                {
                    _orchestrator.SyncFactionsAsync().Wait();
                }
            }
            else
            {
                LoggerUtil.LogDebug("[SYNC] Faction sync disabled - timer fired but skipped");
            }
        }

        /// <summary>
        /// Handles /tds commands from in-game chat
        /// Delegates to CommandProcessor for actual processing
        /// </summary>
        private void HandleChatCommand(string message, long steamId, string playerName)
        {
            try
            {
                LoggerUtil.LogDebug(
                    $"[COMMAND_ROUTE] Routing command to CommandProcessor: {message}"
                );

                if (_commandProcessor != null)
                {
                    _commandProcessor.ProcessCommand(message, steamId, playerName);
                    LoggerUtil.LogInfo($"[COMMAND_ROUTE] CommandProcessor handled: {message}");
                }
                else
                {
                    LoggerUtil.LogError("[COMMAND_ROUTE] CommandProcessor is NULL!");
                    MyVisualScriptLogicProvider.SendChatMessage(
                        "Command processor not available",
                        "TDS",
                        0,
                        "Red"
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[COMMAND_ROUTE] Error routing command: {ex.Message}");
            }
        }

        /// <summary>
        /// Process chat messages to detect player joins/leaves/deaths
        /// This is called from Torch when chat messages arrive
        /// </summary>
        public void ProcessChatMessage(string message, string author, string channel)
        {
            LoggerUtil.LogDebug(
                $@"[CHAT PROCESS] Channel: {channel} | Author: {author} | Message: {message}"
            );

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(author))
            {
                LoggerUtil.LogDebug($"[CHAT PROCESS] - returned due to null/empty");
                return;
            }

            // Prevent duplication: skip Server event messages that are already sent from event handlers
            if (author == "Server")
            {
                // Skip all messages from server to prevent duplication

                /*
                // Skip death messages - already sent from OnCharacterDied
                if (message.Contains("died") || message.Contains("killed"))
                {
                    LoggerUtil.LogDebug(
                        "[CHAT PROCESS] Skipped Server death message to prevent duplication on Discord"
                    );
                    return;
                }

                // Skip join/leave messages - already sent from LogPlayerJoinAsync/LogPlayerLeaveAsync
                if (message.Contains("joined the server") || message.Contains("left the server"))
                {
                    LoggerUtil.LogDebug(
                        "[CHAT PROCESS] Skipped Server join/leave message to prevent duplication on Discord"
                    );
                    return;
                }
                // */
                LoggerUtil.LogDebug(
                    "[CHAT PROCESS] SKIPPED ALL: Skipped Server message to prevent duplication on Discord"
                );
                return;
            }

            // System messages
            if (channel == "System" && _playerTracking != null)
            {
                LoggerUtil.LogDebug("[CHAT PROCESS] Forwarding system message to tracking");
                _playerTracking.ProcessSystemChatMessage(message);
                return;
            }

            // Normal chat → Discord
            if (_chatSync != null && _config?.Chat != null)
            {
                bool enabled = _config.Chat.ServerToDiscord;
                LoggerUtil.LogDebug($"[CHAT PROCESS] ServerToDiscord enabled: {enabled}");

                if (enabled)
                {
                    if (message.StartsWith("/"))
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Skipped command");
                        return;
                    }

                    if (channel == "Global")
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Global chat - sending to Discord");
                        _ = _chatSync.SendGameMessageToDiscordAsync(author, message);
                    }
                    else if (channel.StartsWith("Faction:"))
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Faction chat - skipped for now");
                    }
                    else if (channel == "Private")
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Private chat - skipped for security");
                    }
                    else
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Unknown channel - fallback to global");
                        _ = _chatSync.SendGameMessageToDiscordAsync(author, message);
                    }
                }
                else
                {
                    LoggerUtil.LogDebug("[CHAT PROCESS] ServerToDiscord disabled in config");
                }
            }
            else
            {
                LoggerUtil.LogWarning("[CHAT PROCESS] ChatSyncService or config null");
            }
        }
        public override void Dispose()
        {
            // Cleanup player tracking service
            if (_playerTracking != null)
            {
                _playerTracking.Dispose();
            }

            // Stop timers
            if (_syncTimer != null)
            {
                _syncTimer.Stop();
                _syncTimer.Dispose();
            }

            // Detach Discord bot events
            if (_discordBot != null)
            {
                _discordBot.OnVerificationAttempt -= null;
            }

            try
            {
                var torchInstance = _torch as ITorchServer;
                if (
                    torchInstance != null
                    && torchInstance.CurrentSession != null
                    && torchInstance.CurrentSession.Managers != null
                )
                {
                    var chatManager =
                        torchInstance.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    if (chatManager != null)
                    {
                        chatManager.MessageRecieved -= OnChatMessageProcessing;
                        LoggerUtil.LogInfo("Unhooked chat message handler");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error unhooking chat handler: " + ex.Message);
            }

            base.Dispose();
        }
    }
}
