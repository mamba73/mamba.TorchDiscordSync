// Services/PlayerTrackingService.cs
using System;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Torch.API;
using Torch.Managers.ChatManager;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for tracking player joins/leaves and server status changes
    /// Uses both instant chat detection and polling as fallback
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        private readonly ITorchBase _torch;
        private ChatManagerClient _chatManager;
        private bool _chatReceiverAttached = false;

        // Polling as fallback
        private System.Timers.Timer _pollingTimer;
        private System.Collections.Generic.Dictionary<long, string> _previousPlayers =
            new System.Collections.Generic.Dictionary<long, string>();

        public PlayerTrackingService(EventLoggingService eventLog, ITorchBase torch)
        {
            _eventLog = eventLog;
            _torch = torch;
        }

        /// <summary>
        /// Initialize player tracking - tries instant chat detection first, falls back to polling
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Try to initialize chat receiver for instant detection
                if (InitializeChatReceiver())
                {
                    LoggerUtil.LogSuccess("Player tracking initialized with instant chat detection");
                    return;
                }

                // Fallback to polling if chat receiver fails
                InitializePolling();
                LoggerUtil.LogInfo("Player tracking initialized with polling (5-second intervals)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Player tracking initialization failed: " + ex.Message);

                // Emergency fallback
                InitializePolling();
                LoggerUtil.LogInfo("Player tracking emergency fallback to polling activated");
            }
        }

        /// <summary>
        /// Try to initialize chat receiver for instant detection
        /// </summary>
        private bool InitializeChatReceiver()
        {
            try
            {
                // CORRECTED: Use non-generic GetManager with typeof
                _chatManager = _torch.Managers.GetManager(typeof(ChatManagerClient)) as ChatManagerClient;
                if (_chatManager != null)
                {
                    _chatManager.Attach();
                    _chatReceiverAttached = true;
                    LoggerUtil.LogSuccess("Chat receiver attached for instant player detection");
                    return true;
                }
                else
                {
                    LoggerUtil.LogWarning("ChatManagerClient not available, falling back to polling");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Chat receiver initialization failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Initialize polling as fallback method
        /// </summary>
        private void InitializePolling()
        {
            try
            {
                _pollingTimer = new System.Timers.Timer(5000); // 5 seconds
                _pollingTimer.Elapsed += OnPollingTick;
                _pollingTimer.AutoReset = true;
                _pollingTimer.Start();
                LoggerUtil.LogInfo("Player polling timer started (5-second intervals)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Polling initialization failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Polling tick to detect player changes
        /// </summary>
        private void OnPollingTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckPlayerChanges();
        }

        /// <summary>
        /// Check for player changes and send notifications
        /// </summary>
        private void CheckPlayerChanges()
        {
            try
            {
                if (_eventLog == null || MyAPIGateway.Players == null) return;

                var currentPlayers = new System.Collections.Generic.List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(currentPlayers);

                var currentDict = new System.Collections.Generic.Dictionary<long, string>();
                foreach (var player in currentPlayers)
                {
                    currentDict[(long)player.SteamUserId] = player.DisplayName; // CONVERT TO LONG
                }

                // Check for new players (joined)
                foreach (var kvp in currentDict)
                {
                    if (!_previousPlayers.ContainsKey(kvp.Key))
                    {
                        // New player joined
                        string playerName = kvp.Value ?? "Unknown";
                        long steamId = kvp.Key;

                        LoggerUtil.LogInfo($"Player joined (polling): {playerName} ({steamId})");

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerJoinAsync(playerName, steamId);
                        });
                    }
                }

                // Check for disconnected players (left)
                foreach (var kvp in _previousPlayers)
                {
                    if (!currentDict.ContainsKey(kvp.Key))
                    {
                        // Player left
                        string playerName = kvp.Value ?? "Unknown";
                        long steamId = kvp.Key;

                        LoggerUtil.LogInfo($"Player left (polling): {playerName} ({steamId})");

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerLeaveAsync(playerName, steamId);
                        });
                    }
                }

                // Update previous players list
                _previousPlayers = new System.Collections.Generic.Dictionary<long, string>(currentDict);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error checking player changes: " + ex.Message);
            }
        }

        /// <summary>
        /// Process system chat messages to detect player joins/leaves instantly
        /// Called from main plugin 
        /// </summary>
        public void ProcessSystemChatMessage(string message)
        {
            try
            {
                if (_eventLog == null || string.IsNullOrEmpty(message)) return;

                // Check for player join messages
                if (message.EndsWith(" joined the game"))
                {
                    string playerName = message.Replace(" joined the game", "").Trim();
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        LoggerUtil.LogInfo($"Player joined (instant): {playerName}");
                        Task.Run(async () =>
                        {
                            // USE 0 AS STEAM ID FOR SYSTEM MESSAGES
                            await _eventLog.LogPlayerJoinAsync(playerName, 0);
                        });
                    }
                }
                // Check for player leave messages
                else if (message.EndsWith(" left the game"))
                {
                    string playerName = message.Replace(" left the game", "").Trim();
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        LoggerUtil.LogInfo($"Player left (instant): {playerName}");
                        Task.Run(async () =>
                        {
                            // USE 0 AS STEAM ID FOR SYSTEM MESSAGES
                            await _eventLog.LogPlayerLeaveAsync(playerName, 0);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error processing system chat message: " + ex.Message);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            // Detach chat manager
            if (_chatManager != null && _chatReceiverAttached)
            {
                try
                {
                    _chatManager.Detach();
                    _chatReceiverAttached = false;
                    LoggerUtil.LogInfo("Chat receiver detached");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Error detaching chat receiver: " + ex.Message);
                }
            }

            // Stop polling timer
            if (_pollingTimer != null)
            {
                try
                {
                    _pollingTimer.Stop();
                    _pollingTimer.Dispose();
                    LoggerUtil.LogInfo("Player polling timer stopped");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Error stopping polling timer: " + ex.Message);
                }
            }
        }
    }
}
