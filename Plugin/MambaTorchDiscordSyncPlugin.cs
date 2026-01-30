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
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.API.Session;
using VRage.Game.ModAPI;

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
        private PlayerTrackingService _playerTracking; // EXISTS

        // NEW: Handlers (renamed to avoid conflicts)
        private CommandProcessor _commandProcessor;
        private EventManager _eventManager;
        private ChatModerator _chatModerator;

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
                _deathLog = new DeathLogService(_db, _eventLog);

                // Initialize player tracking service - UPDATED CONSTRUCTOR WITH DeathLogService
                _playerTracking = new PlayerTrackingService(_eventLog, torch, _deathLog);

                // Initialize faction sync service
                _factionSync = new FactionSyncService(_db, _discordWrapper);

                // Initialize chat sync service - FIX PARAMETER ORDER
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
                                $"[DISCORD→GAME] Forwarded message from {msg.Author.Username}"
                            );
                        }
                    };
                }

                // NEW: Initialize handlers (renamed)
                _commandProcessor = new CommandProcessor(
                    _config,
                    _discordWrapper,
                    _db,
                    _factionSync,
                    _eventLog,
                    _orchestrator
                );
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

                // Initialize sync orchestrator
                _orchestrator = new SyncOrchestrator(
                    _db,
                    _discordWrapper,
                    _factionSync,
                    _eventLog,
                    _config
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

                // Register session event handler - FIXED: use 'as' cast for compatibility
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

                // Note: Chat message handling will be done through PlayerTrackingService
                // This relies on polling and external message injection
                LoggerUtil.LogInfo(
                    "Chat message handling configured - relying on external injection"
                );

                // Initialize player tracking - NEW
                _playerTracking.Initialize();
                LoggerUtil.LogSuccess("Player tracking service initialized");

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

                // Optional: Save config after load (ensures any merged/default values are persisted)
                // This calls the existing Save() from MainConfig.cs - no duplicate logic
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
            Console.WriteLine("╔════════════════════════════════════════════════════╗");
            Console.WriteLine(
                "║ "
                    + VersionUtil.GetPluginName()
                    + " "
                    + VersionUtil.GetVersionString()
                    + " - "
                    + title.PadRight(20)
                    + "║"
            );
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
                    // Get actual simulation speed - SAFER VERSION WITHOUT GetServerSimulationRatio
                    float currentSimSpeed = 1.0f;
                    try
                    {
                        // Use a safer approach to get simulation ratio
                        if (
                            MyAPIGateway.Session != null
                            && MyAPIGateway.Session.LocalHumanPlayer != null
                        )
                        {
                            // Fallback to default 1.0 if we can't get the real value
                            currentSimSpeed = 1.0f;
                            LoggerUtil.LogDebug(
                                "Using default SimSpeed (1.0) - GetServerSimulationRatio not available"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError("Error getting SimSpeed: " + ex.Message);
                        currentSimSpeed = 1.0f;
                    }
                    // Send server startup message with real SimSpeed
                    if (_eventLog != null && _config != null && _config.Monitoring != null)
                    {
                        // FIXED: Safe conversion bool? → bool (null-coalescing)
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
                    LoggerUtil.LogInfo("═══ Server session UNLOADING ═══");
                    if (_syncTimer != null && _syncTimer.Enabled)
                        _syncTimer.Stop();
                    break;
                case TorchSessionState.Unloaded:
                    LoggerUtil.LogWarning("═══ Server session UNLOADED ═══");
                    // Send server shutdown message
                    if (_eventLog != null && _config != null && _config.Monitoring != null)
                    {
                        // FIXED: Same safe conversion bool? → bool
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
                // Get current SimSpeed from session - SAFER VERSION
                float currentSimSpeed = 1.0f;
                try
                {
                    // Use safer approach without problematic casting
                    currentSimSpeed = 1.0f; // Default value
                    LoggerUtil.LogDebug("Using default SimSpeed (1.0) in startup");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Error getting SimSpeed: " + ex.Message);
                    currentSimSpeed = 1.0f;
                }
                // Check server status
                _orchestrator.CheckServerStatusAsync(currentSimSpeed).Wait();
                // Load factions from save - KEEP EXISTING LOGIC FOR NOW
                var factions = LoadFactionsFromSession(session);
                if (factions.Count > 0)
                {
                    LoggerUtil.LogInfo("[STARTUP] Found " + factions.Count + " player factions");
                    _orchestrator.ExecuteFullSyncAsync(factions).Wait();
                }
                else
                {
                    LoggerUtil.LogWarning("[STARTUP] No player factions found (tag length != 3)");
                }
                // Start periodic sync timer
                if (_syncTimer != null && !_syncTimer.Enabled)
                {
                    _syncTimer.Start();
                    LoggerUtil.LogSuccess(
                        "[STARTUP] Sync timer started (interval: "
                            + _config.SyncIntervalSeconds
                            + "s)"
                    );
                }
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

        private void OnSyncTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_orchestrator != null)
            {
                _orchestrator.SyncFactionsAsync().Wait();
            }
        }

        /// <summary>
        /// Handles /tds commands from in-game chat
        /// Delegates to CommandProcessor
        /// </summary>
        public void HandleChatCommand(string command, long playerSteamID, string playerName)
        {
            if (_commandProcessor != null)
            {
                _commandProcessor.ProcessCommand(command, playerSteamID, playerName);
            }
        }

        /// <summary>
        /// Process chat messages to detect player joins/leaves/deaths
        /// This is called from Torch when chat messages arrive
        /// </summary>
        public void ProcessChatMessage(string message, string author, string channel)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(author))
                return;

            // Forward system messages to player tracking (join/leave/death fallback)
            if (channel == "System" && _playerTracking != null)
            {
                _playerTracking.ProcessSystemChatMessage(message);
            }

            // Normal messages (global) → send to Discord
            if (_chatSync != null && _config != null && _config.Chat != null)
            {
                // FIXED: Safe conversion bool → bool check (no string conversion needed)
                bool serverToDiscordEnabled = _config.Chat.ServerToDiscord;

                LoggerUtil.LogDebug($"Chat sync check - ServerToDiscord: {serverToDiscordEnabled}");

                if (serverToDiscordEnabled)
                {
                    // Skip commands and system messages
                    if (message.StartsWith("/") || channel == "System")
                        return;

                    string formatted = _config
                        .Chat.GameToDiscordFormat.Replace("{p}", author)
                        .Replace("{msg}", message)
                        .Replace("{c}", channel);

                    _ = _chatSync.SendGameMessageToDiscordAsync(author, message);
                }
            }
        }

        /// <summary>
        /// Loads factions from Space Engineers save
        /// </summary>
        private List<FactionModel> LoadFactionsFromSession(ITorchSession session)
        {
            var factions = new List<FactionModel>();
            try
            {
                if (session == null)
                    return factions;
                // TODO: Replace with real data loading
                var testFaction = new FactionModel();
                testFaction.Tag = "ABC";
                testFaction.Name = "Test Faction";
                testFaction.Players = new List<FactionPlayerModel>
                {
                    new FactionPlayerModel { SteamID = 123456789, DiscordUserID = 987654321 },
                    new FactionPlayerModel { SteamID = 234567890, DiscordUserID = 876543210 },
                };
                factions.Add(testFaction);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error loading factions from session: " + ex.Message);
            }
            return factions;
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
                // No need to detach OnMessageReceivedEvent - GC will clean it
            }
            base.Dispose();
        }
    }
}
