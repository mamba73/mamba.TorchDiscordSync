// Plugin/index.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord.WebSocket;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Core;
using mamba.TorchDiscordSync.Plugin.Handlers;
using mamba.TorchDiscordSync.Plugin.Models;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Managers.ChatManager;

namespace mamba.TorchDiscordSync
{
    /// <summary>
    /// mamba.TorchDiscordSync.Plugin - Advanced Space Engineers faction/Discord sync plugin
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
        private DeathMessageHandler _deathMessageHandler;
        private PlayerTrackingService _playerTracking;
        private DamageTrackingService _damageTracking;
        private bool _damageTrackingInitialized = false;
        private MonitoringService _monitoringService;

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
                PluginUtils.PrintBanner("INITIALIZING");

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

                /*
                // Initialize SQLite database (optional, for future use)
                var sqliteDb = new SQLiteDatabaseService();
                if (sqliteDb.Initialize())
                {
                    LoggerUtil.LogSuccess("[INIT] SQLite database initialized");
                }
                else
                {
                    LoggerUtil.LogWarning("[INIT] SQLite database initialization failed (non-critical)");
                }
                //*/


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
                _verification = new VerificationService(
                    _db,
                    _config.VerificationCodeExpirationMinutes
                );

                // Initialize event logging service
                _eventLog = new EventLoggingService(_db, _discordWrapper, _config);

                // Initialize death log service FIRST so we can pass it to PlayerTrackingService
                // NEW: Pass MainConfig to DeathLogService for location zones configuration
                _deathLog = new DeathLogService(_db, _eventLog, _config);

                try
                {
                    _damageTracking = new DamageTrackingService(_config); 
                    LoggerUtil.LogInfo(
                        "[INIT] DamageTrackingService instance created (Init deferred to session load)"
                    );
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError(
                        $"[INIT] Failed to create DamageTrackingService: {ex.Message}"
                    );
                    _damageTracking = null;
                }

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

                // 1. Create DeathMessageHandler FIRST
                _deathMessageHandler = new DeathMessageHandler(_eventLog, _config, _damageTracking);
                LoggerUtil.LogDebug("[INIT] DeathMessageHandler created");

                // 2. THEN create PlayerTrackingService with it
                _playerTracking = new PlayerTrackingService(
                    _eventLog,
                    _torch,
                    _deathLog,
                    _config,
                    _deathMessageHandler // ← NOW it's NOT NULL!
                );
                LoggerUtil.LogDebug("[INIT] PlayerTrackingService initialized");

                // Initialize faction sync service
                _factionSync = new FactionSyncService(_db, _discordWrapper, _config);

                // Initialize chat sync service
                _chatSync = new ChatSyncService(_discordWrapper, _config, _db);

                // Hook Discord messages for chat sync using public event
                if (_discordBot != null)
                {
                    _discordBot.OnMessageReceivedEvent += async msg =>
                    {
                        if (msg.Author.IsBot || msg.Content.StartsWith(_config.Discord.BotPrefix))
                            return;

                        if (msg.Channel is SocketTextChannel textChannel)
                        {
                            ulong channelId = textChannel.Id;
                            // Global chat channel → game global
                            if (channelId == _config.Discord.ChatChannelId)
                            {
                                await _chatSync.SendDiscordMessageToGameAsync(
                                    msg.Author.Username,
                                    msg.Content
                                );
                                LoggerUtil.LogDebug(
                                    $"[DISCORD›GAME] Forwarded message from {msg.Author.Username}"
                                );
                                return;
                            }
                            // Faction channel (synced) → game faction only (secure)
                            var factions = _db?.GetAllFactions();
                            if (factions != null)
                            {
                                var faction = factions.FirstOrDefault(f => f.DiscordChannelID == channelId);
                                if (faction != null)
                                {
                                    await _chatSync.SendDiscordMessageToFactionInGameAsync(
                                        faction.FactionID,
                                        msg.Author.Username,
                                        msg.Content
                                    );
                                    LoggerUtil.LogDebug(
                                        $"[DISCORD›FACTION] {faction.Tag} from {msg.Author.Username}"
                                    );
                                }
                            }
                        }
                    };
                }

                // Initialize sync orchestrator
                // FIXED: Proslijedi _factionReader za učitavanje stvarnih frakcija
                _orchestrator = new SyncOrchestrator(
                    _db,
                    _discordWrapper,
                    _factionSync,
                    _eventLog,
                    _config
                );

                // Initialize verification command handler BEFORE CommandProcessor
                _verificationCommandHandler = new VerificationCommandHandler(
                    _verification,
                    _eventLog,
                    _config,
                    _discordBot,
                    _discordBotConfig
                );
                LoggerUtil.LogInfo("[INIT] VerificationCommandHandler created and ready");

                // Initialize handlers - NOW pass _verificationCommandHandler to CommandProcessor!
                _commandProcessor = new CommandProcessor(
                    _config,
                    _discordWrapper,
                    _db,
                    _factionSync,
                    _eventLog,
                    _orchestrator,
                    _verification,
                    _verificationCommandHandler
                );
                LoggerUtil.LogInfo(
                    "[INIT] CommandProcessor created with VerificationCommandHandler"
                );

                _eventManager = new EventManager(_config, _discordWrapper, _eventLog);
                _chatModerator = new ChatModerator(_config, _discordWrapper, _db);

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
                int syncInterval = 5000;
                if (_config != null)
                {
                    syncInterval = _config.SyncIntervalSeconds * 1000;
                }

                // Check if faction sync is enabled before creating timer
                if (
                    syncInterval <= 0
                    || (_config != null && _config.Faction != null && !_config.Faction.Enabled)
                )
                {
                    LoggerUtil.LogInfo(
                        "Faction sync timer NOT created - disabled or interval is 0"
                    );
                }
                else
                {
                    _syncTimer = new Timer(syncInterval);
                    _syncTimer.Elapsed += OnSyncTimerElapsed;
                    _syncTimer.AutoReset = true;
                    LoggerUtil.LogInfo($"Faction sync timer created (interval: {syncInterval}ms)");
                }

                _isInitialized = true;
                PluginUtils.PrintBanner("INITIALIZATION COMPLETE");

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
        /// Process incoming Torch chat messages
        /// Filter and route based on channel and content
        ///
        /// Priority order:
        /// 1. Detect and handle /tds commands (with proper SteamID)
        /// 2. Filter out faction chat (security - don't leak to Discord)
        /// 3. Filter out Server-authored messages (already sent to Discord separately)
        /// 4. Filter out private command responses (marked with [PRIVATE_CMD])
        /// 5. Forward global chat to Discord sync
        /// </summary>
        private void OnChatMessageProcessing(TorchChatMessage msg, ref bool consumed)
        {
            try
            {
                if (string.IsNullOrEmpty(msg.Message))
                    return;

                string channelName = msg.Channel.ToString() ?? "Unknown";

                LoggerUtil.LogDebug($"[CHAT] Channel: {channelName}, Message: {msg.Message}");

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

                // PRIORITY 4: Filter out Server-authored messages
                // These are join/leave/death messages that were already sent to Discord directly
                // by EventLoggingService.LogPlayerJoinAsync(), LogPlayerLeaveAsync(), or DeathMessageHandler
                // Skipping them here prevents duplicate messages on Discord
                if (msg.Author == "Server")
                {
                    LoggerUtil.LogDebug(
                        $"[CHAT] Skipped Server message (already sent to Discord): {msg.Message}"
                    );
                    return; // Stop processing - don't duplicate on Discord
                }

                // PRIORITY 5: Filter out private command responses
                // Command responses (help text, verification codes, etc.) are prefixed with [PRIVATE_CMD]
                // to indicate they should NOT be forwarded to Discord
                if (ChatUtils.IsPrivateMessage(msg.Message))
                {
                    LoggerUtil.LogDebug($"[CHAT] Skipped private command response: {msg.Message}");
                    return; // Stop processing - don't forward private responses to Discord
                }

                // PRIORITY 6: Prevent Discord loop messages (for safety)
                if (msg.Message.StartsWith("[Discord] ") || msg.Message.StartsWith("Discord"))
                {
                    LoggerUtil.LogDebug("[CHAT] Skipped Discord loop message: " + msg.Message);
                    return; // Prevent Discord loop messages
                }

                // PRIORITY 7: Forward ONLY global chat to Discord sync
                if (channelName == "Global" || channelName.StartsWith("Global"))
                {
                    LoggerUtil.LogDebug($"[CHAT] Forwarding global chat to Discord: {msg.Message}");
                    ChatUtils.ProcessChatMessage(
                        msg.Message,
                        msg.Author,
                        "Global",
                        _chatSync,
                        _playerTracking,
                        _config
                    );
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

        // Handles verification attempts from Discord users. Sends DM first, then in-game notification.
        private Task HandleVerificationAsync(string code, ulong discordID, string discordUsername)
        {
            if (_verificationCommandHandler != null)
            {
                return _verificationCommandHandler
                    .VerifyFromDiscordAsync(code, discordID, discordUsername)
                    .ContinueWith(async t =>
                    {
                        try
                        {
                            var result = t.Result;
                            LoggerUtil.LogInfo("[VERIFY] Verification result: " + result.Message);

                            bool isSuccess = result.IsSuccess;
                            var verifiedPlayer = isSuccess ? _db?.GetVerifiedPlayerByDiscordID(discordID) : null;

                            if (isSuccess && verifiedPlayer != null)
                            {
                                if (_discordBot != null)
                                {
                                    ulong verifiedRoleId =
                                        await _discordBot.GetOrCreateVerifiedRoleAsync();
                                    if (verifiedRoleId != 0)
                                    {
                                        _config.Discord.VerifiedRoleId = verifiedRoleId;
                                        _config.Save();

                                        var discordClient = _discordBot.GetClient();
                                        if (discordClient != null)
                                        {
                                            var discordUser = await discordClient.GetUserAsync(discordID);
                                            if (discordUser != null)
                                            {
                                                bool roleAssigned =
                                                    await _discordBot.AssignVerifiedRoleAsync(
                                                        discordUser,
                                                        verifiedRoleId
                                                    );
                                                if (roleAssigned)
                                                    LoggerUtil.LogSuccess(
                                                        $"[VERIFY] Assigned Verified role to {discordUsername}"
                                                    );
                                            }
                                        }
                                    }

                                    if (_db != null)
                                    {
                                        var playerFaction = _db.GetAllFactions()
                                            ?.FirstOrDefault(f =>
                                                f.Players != null
                                                && f.Players.Any(p => p.SteamID == verifiedPlayer.SteamID)
                                            );

                                        if (playerFaction != null)
                                        {
                                            var discordClient = _discordBot.GetClient();
                                            if (discordClient != null)
                                            {
                                                var discordUser = await discordClient.GetUserAsync(discordID);
                                                if (discordUser != null)
                                                {
                                                    bool factionRoleAssigned =
                                                        await _discordBot.AssignFactionRoleAsync(
                                                            discordUser,
                                                            playerFaction.Tag
                                                        );
                                                    if (factionRoleAssigned)
                                                        LoggerUtil.LogSuccess(
                                                            $"[VERIFY] Assigned faction role '{playerFaction.Tag}' to {discordUsername}"
                                                        );
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // 1) Send result DM to Discord user first
                            if (_discordBot != null)
                            {
                                await _discordBot.SendVerificationResultDMAsync(
                                    discordUsername,
                                    discordID,
                                    result.Message,
                                    isSuccess
                                );
                                LoggerUtil.LogDebug(
                                    "[VERIFY] Sent verification result DM to " + discordUsername
                                );
                            }

                            // 2) Then send in-game private message to player (success or error when we have SteamID)
                            if (result.SteamIdForNotify.HasValue && !string.IsNullOrEmpty(result.GamePlayerName))
                            {
                                SendInGameNotification(
                                    result.SteamIdForNotify.Value,
                                    result.GamePlayerName,
                                    isSuccess,
                                    result.Message
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError(
                                "[VERIFY] Error in HandleVerificationAsync: " + ex.Message
                            );
                        }
                    });
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// NEW: Send in-game notification to player about verification result
        /// </summary>
        private void SendInGameNotification(
            long steamID,
            string playerName,
            bool isSuccess,
            string message
        )
        {
            try
            {
                var players = new System.Collections.Generic.List<VRage.Game.ModAPI.IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);

                var player = players.FirstOrDefault(p => (long)p.SteamUserId == steamID);
                if (player == null)
                    return;

                var character = player.Character;
                if (character == null)
                    return;

                string notificationMsg = isSuccess
                    ? "✅ Verification successful! Discord account linked."
                    : $"❌ Verification failed: {message}";

                Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(
                    notificationMsg,
                    "TDS",
                    character.EntityId,
                    "Blue"
                );
                LoggerUtil.LogSuccess($"[VERIFY_NOTIFY] Sent to {playerName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_NOTIFY] Error: {ex.Message}");
            }
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

                    // 1. Initialize DamageTrackingService (if not already initialized)
                    if (_damageTracking != null && !_damageTrackingInitialized)
                    {
                        try
                        {
                            _damageTracking.Init();
                            _damageTrackingInitialized = true; // ← KORISTI flag!
                            LoggerUtil.LogSuccess(
                                "[DAMAGE_TRACK] DamageTrackingService initialized"
                            );
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError(
                                $"[DAMAGE_TRACK] Failed to initialize: {ex.Message}"
                            );
                        }
                    }

                    // 2: Initialize KillerDetectionService
                    if (_deathMessageHandler != null)
                    {
                        try
                        {
                            _deathMessageHandler.InitializeKillerDetection();
                            LoggerUtil.LogSuccess("[KILLER_DETECTION] Service initialized");
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError(
                                $"[KILLER_DETECTION] Failed to initialize: {ex.Message}"
                            );
                        }
                    }

                    // 3. Initialize PlayerTrackingService NOW when session is loaded
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

                    //  4 ostalo...
                    //  Hook chat commands NOW when session is loaded
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

                    // 5. Initialize MonitoringService for SimSpeed and player count monitoring
                    try
                    {
                        if (_monitoringService == null && _discordBot != null)
                        {
                            _monitoringService = new MonitoringService(_config, _discordBot);
                            _monitoringService.Initialize();
                            LoggerUtil.LogSuccess(
                                "[MONITORING] MonitoringService initialized after session load"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            $"[MONITORING] Failed to initialize MonitoringService: {ex.Message}"
                        );
                    }

                    // OPTIMIZED: Get simulation speed using helper method
                    float currentSimSpeed = PluginUtils.GetCurrentSimSpeed();

                    // Send server startup message with real SimSpeed
                    if (_eventLog != null && _config != null && _config.Monitoring != null)
                    {
                        bool monitoringEnabled = _config?.Monitoring?.Enabled == true;
                        if (monitoringEnabled)
                        {
                            // Additionally, send a stable SimSpeed after a short delay
                            Task.Run(async () =>
                            {
                                // Wait 3 seconds for physics to stabilize and avoid NaN
                                await Task.Delay(3000);
                                float stableSimSpeed = PluginUtils.GetCurrentSimSpeed();
                                await _eventLog.LogServerStatusAsync("STARTED", stableSimSpeed);
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
                    // Stop MonitoringService
                    if (_monitoringService != null)
                    {
                        _monitoringService.Stop();
                        LoggerUtil.LogInfo("[MONITORING] MonitoringService stopped");
                    }
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

                // NEW: Delay status reporting to ensure physics are running and SimSpeed is stable
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait 5 seconds for the server to settle
                        await Task.Delay(5000);

                        float stableSimSpeed = PluginUtils.GetCurrentSimSpeed();

                        // Now call the status check with a real number
                        if (_orchestrator != null)
                        {
                            await _orchestrator.CheckServerStatusAsync(stableSimSpeed);
                            LoggerUtil.LogSuccess(
                                $"[STARTUP] Post-load status reported. SimSpeed: {stableSimSpeed:F2}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError("Error in delayed status check: " + ex.Message);
                    }
                });

                // Load real factions from game using FactionReaderService
                var factions = _factionSync.LoadFactionsFromGame();
                if (factions.Count > 0)
                {
                    LoggerUtil.LogInfo("[STARTUP] Found " + factions.Count + " player factions");
                    // disabled
                    _orchestrator.ExecuteFullSyncAsync(factions).Wait();
                }
                else
                {
                    LoggerUtil.LogWarning("[STARTUP] No player factions found (tag length != 3)");
                }

                // Start periodic sync timer (only if enabled and timer was created)
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

        /// <summary>
        /// Periodic faction sync timer handler
        /// Only runs if faction sync is enabled in config
        /// </summary>
        private void OnSyncTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Double-check that faction sync is enabled before running
            if (_config != null && _config.Faction != null && _config.Faction.Enabled)
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
        public void HandleChatCommand(string command, long playerSteamID, string playerName)
        {
            if (_commandProcessor != null)
            {
                LoggerUtil.LogDebug($"[COMMAND] Forwarding to CommandProcessor: {command}");
                _commandProcessor.ProcessCommand(command, playerSteamID, playerName);
            }
            else
            {
                LoggerUtil.LogError("[COMMAND] CommandProcessor is null - cannot process command");
            }
        }

        public override void Dispose()
        {
            // Cleanup death message handler
            if (_deathMessageHandler != null)
            {
                _deathMessageHandler.Cleanup();
            }

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

            // Cleanup monitoring service
            if (_monitoringService != null)
            {
                _monitoringService.Dispose();
                _monitoringService = null;
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
