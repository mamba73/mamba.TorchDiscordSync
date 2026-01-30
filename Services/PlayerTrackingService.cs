// Services/PlayerTrackingService.cs
using System;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Torch.API;
using Torch.Managers.ChatManager;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;
using VRage.Game.ModAPI;
using System.Collections.Generic;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for tracking player joins/leaves, deaths and server status changes
    /// Uses polling-based detection since event handlers are problematic
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        private readonly ITorchBase _torch;
        private readonly DeathLogService _deathLog;
        
        // Polling timers
        private System.Timers.Timer _pollingTimer;
        private System.Timers.Timer _deathCheckTimer;
        
        // Player tracking
        private System.Collections.Generic.Dictionary<ulong, string> _previousPlayers =
            new System.Collections.Generic.Dictionary<ulong, string>();
        
        // Death tracking
        private Dictionary<ulong, DateTime> _lastPlayerSeen = new Dictionary<ulong, DateTime>();
        private HashSet<string> _processedDeathMessages = new HashSet<string>();

        public PlayerTrackingService(EventLoggingService eventLog, ITorchBase torch, DeathLogService deathLog)
        {
            _eventLog = eventLog;
            _torch = torch;
            _deathLog = deathLog;
        }

        /// <summary>
        /// Initialize player tracking with polling only
        /// </summary>
        public void Initialize()
        {
            try
            {
                LoggerUtil.LogDebug("Initializing PlayerTrackingService with polling only...");
                
                // Initialize main polling for join/leave detection
                InitializePolling();
                
                // Initialize death checking
                InitializeDeathChecking();
                
                LoggerUtil.LogInfo("Player tracking initialized with polling (5-second intervals)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Player tracking initialization failed: " + ex.Message);
                InitializePolling();
                LoggerUtil.LogInfo("Player tracking emergency fallback to polling activated");
            }
        }

        /// <summary>
        /// Initialize main polling for player join/leave detection
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
        /// Initialize death checking timer
        /// </summary>
        private void InitializeDeathChecking()
        {
            try
            {
                _deathCheckTimer = new System.Timers.Timer(3000); // 3 seconds
                _deathCheckTimer.Elapsed += OnDeathCheckTick;
                _deathCheckTimer.AutoReset = true;
                _deathCheckTimer.Start();
                LoggerUtil.LogInfo("Death checking timer started (3-second intervals)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Death checking initialization failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Main polling tick to detect player changes
        /// </summary>
        private void OnPollingTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckPlayerChanges();
        }

        /// <summary>
        /// Death checking tick - monitor for death messages in chat log
        /// </summary>
        private void OnDeathCheckTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckForDeathMessages();
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

                var currentDict = new System.Collections.Generic.Dictionary<ulong, string>();
                foreach (var player in currentPlayers)
                {
                    currentDict[player.SteamUserId] = player.DisplayName;
                    
                    // Update last seen time for death tracking
                    _lastPlayerSeen[player.SteamUserId] = DateTime.UtcNow;
                }

                // Check for new players (joined)
                foreach (var kvp in currentDict)
                {
                    if (!_previousPlayers.ContainsKey(kvp.Key))
                    {
                        // New player joined
                        string playerName = kvp.Value ?? "Unknown";
                        ulong steamId = kvp.Key;

                        LoggerUtil.LogInfo($"Player joined (polling): {playerName} ({steamId})");

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerJoinAsync(playerName, (long)steamId);
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
                        ulong steamId = kvp.Key;

                        LoggerUtil.LogInfo($"Player left (polling): {playerName} ({steamId})");

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerLeaveAsync(playerName, (long)steamId);
                        });
                        
                        // Remove from death tracking
                        _lastPlayerSeen.Remove(kvp.Key);
                    }
                }

                // Update previous players list
                _previousPlayers = new System.Collections.Generic.Dictionary<ulong, string>(currentDict);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error checking player changes: " + ex.Message);
            }
        }

        /// <summary>
        /// Check for death messages by monitoring chat or player status
        /// </summary>
        private void CheckForDeathMessages()
        {
            try
            {
                // This is a placeholder - in a real implementation we would check
                // for actual death events or parse chat logs
                // For now, we rely on external calls to ProcessSystemChatMessage
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error checking for death messages: " + ex.Message);
            }
        }

        /// <summary>
        /// Process system chat messages to detect player joins/leaves/deaths
        /// This method should be called from the main plugin when chat messages arrive
        /// </summary>
        public void ProcessSystemChatMessage(string message)
        {
            try
            {
                LoggerUtil.LogDebug($"Processing system chat message: '{message}'");

                if (_eventLog == null || string.IsNullOrEmpty(message)) 
                {
                    LoggerUtil.LogDebug("EventLog is null or message is empty");
                    return;
                }

                // Prevent duplicate processing
                if (_processedDeathMessages.Contains(message))
                    return;

                // Check for player join messages
                if (message.EndsWith(" joined the game"))
                {
                    string playerName = message.Replace(" joined the game", "").Trim();
                    LoggerUtil.LogDebug($"Detected join message for: {playerName}");
                    
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        LoggerUtil.LogInfo($"Player joined (instant): {playerName}");
                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerJoinAsync(playerName, 0);
                        });
                    }
                }
                // Check for player leave messages
                else if (message.EndsWith(" left the game"))
                {
                    string playerName = message.Replace(" left the game", "").Trim();
                    LoggerUtil.LogDebug($"Detected leave message for: {playerName}");
                    
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        LoggerUtil.LogInfo($"Player left (instant): {playerName}");
                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerLeaveAsync(playerName, 0);
                        });
                    }
                }
                // Check for player death messages
                else if ((message.Contains(" died") || message.Contains(" was killed")) && 
                         !_processedDeathMessages.Contains(message))
                {
                    LoggerUtil.LogDebug($"Detected death message: {message}");
                    
                    // Add to processed messages to prevent duplicates
                    _processedDeathMessages.Add(message);
                    
                    // Clean up old processed messages (keep only last 100)
                    if (_processedDeathMessages.Count > 100)
                    {
                        // Get first element to remove
                        var enumerator = _processedDeathMessages.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            var first = enumerator.Current;
                            _processedDeathMessages.Remove(first);
                        }
                    }
                    
                    // Log death message
                    if (_deathLog != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Extract victim name from message
                                string victimName = message.Contains(" died") ? 
                                    message.Substring(0, message.IndexOf(" died")) :
                                    message.Substring(0, message.IndexOf(" was killed"));
                                
                                await _deathLog.LogPlayerDeathAsync(
                                    "Unknown", 
                                    victimName, 
                                    "Unknown Weapon", 
                                    0, 
                                    0, 
                                    "Unknown Location"
                                );
                            }
                            catch (Exception ex)
                            {
                                LoggerUtil.LogError("Error in death logging task: " + ex.Message);
                            }
                        });
                    }
                    
                    // Send to Discord through event log
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _eventLog.LogDeathAsync(message);
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError("Error sending death message to Discord: " + ex.Message);
                        }
                    });
                }
                else
                {
                    LoggerUtil.LogDebug($"Unrecognized system message: {message}");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error processing system chat message: " + ex.Message);
                LoggerUtil.LogError("Message content: " + message);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            // Stop death checking timer
            if (_deathCheckTimer != null)
            {
                try
                {
                    _deathCheckTimer.Stop();
                    _deathCheckTimer.Dispose();
                    LoggerUtil.LogInfo("Death checking timer stopped");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Error stopping death checking timer: " + ex.Message);
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
            
            // Clear tracking collections
            _lastPlayerSeen.Clear();
            _processedDeathMessages.Clear();
            _previousPlayers.Clear();
        }
    }
}
