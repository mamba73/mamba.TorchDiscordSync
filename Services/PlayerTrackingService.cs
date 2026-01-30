// Services/PlayerTrackingService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game; // Za MyVisualScriptLogicProvider (slanje u global chat)
using Sandbox.ModAPI;
using Torch.API;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for tracking player joins/leaves and DEATHS via IMyCharacter.CharacterDied event
    /// Sends death messages to global chat in game and Discord.
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        private readonly ITorchBase _torch;
        private readonly DeathLogService _deathLog;

        // Polling timer for join/leave detection only
        private System.Timers.Timer _pollingTimer;

        // Track hooked characters to prevent double-hooking
        private HashSet<ulong> _knownPlayers = new HashSet<ulong>();
        private Dictionary<ulong, IMyCharacter> _trackedCharacters =
            new Dictionary<ulong, IMyCharacter>();

        // Processed deaths to prevent duplicates
        private HashSet<string> _processedDeaths = new HashSet<string>();

        private object _lockObject = new object();

        public PlayerTrackingService(
            EventLoggingService eventLog,
            ITorchBase torch,
            DeathLogService deathLog
        )
        {
            _eventLog = eventLog;
            _torch = torch;
            _deathLog = deathLog;
        }

        /// <summary>
        /// Initialize player tracking
        /// Hook CharacterDied events for existing players
        /// </summary>
        public void Initialize()
        {
            try
            {
                LoggerUtil.LogDebug(
                    "Initializing PlayerTrackingService with CharacterDied event hooking..."
                );

                // Initialize polling for join/leave detection (5 seconds)
                InitializePolling();

                // Hook death events for all current players
                InitializeDeathTracking();

                LoggerUtil.LogSuccess("Player tracking initialized (event-based death detection)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Player tracking initialization failed: " + ex.Message);
                LoggerUtil.LogError("Stack: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// Initialize polling for player join/leave detection
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
        /// Hook death tracking for all current players
        /// Called once at startup and periodically for new joiners
        /// </summary>
        private void InitializeDeathTracking()
        {
            try
            {
                if (MyAPIGateway.Players == null)
                {
                    LoggerUtil.LogWarning(
                        "MyAPIGateway.Players is null - cannot initialize death tracking"
                    );
                    return;
                }

                var allPlayers = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(allPlayers);

                if (allPlayers == null || allPlayers.Count == 0)
                {
                    LoggerUtil.LogDebug("No players found for death tracking initialization");
                    return;
                }

                foreach (var player in allPlayers)
                {
                    try
                    {
                        if (player == null || player.Character == null)
                            continue;

                        HookCharacterDeath(player.Character, player.SteamUserId);
                        _knownPlayers.Add(player.SteamUserId);
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogDebug($"Error hooking player character: {ex.Message}");
                    }
                }

                LoggerUtil.LogInfo($"Death tracking hooked for {_knownPlayers.Count} players");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error initializing death tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Hook CharacterDied event on a specific character
        /// </summary>
        private void HookCharacterDeath(IMyCharacter character, ulong steamId)
        {
            try
            {
                if (character == null)
                    return;

                // Prevent double-hooking
                if (_trackedCharacters.ContainsKey(steamId))
                    return;

                // Hook the CharacterDied event
                character.CharacterDied += (deadChar) => OnCharacterDied(deadChar, steamId);

                // Track this character
                _trackedCharacters[steamId] = character;

                LoggerUtil.LogDebug($"Hooked CharacterDied event for SteamID {steamId}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error hooking character death: {ex.Message}");
            }
        }

        /// <summary>
        /// Main polling tick - check for new/left players
        /// </summary>
        private void OnPollingTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckPlayerChanges();
            HookNewPlayers();
        }

        /// <summary>
        /// Check for player joins/leaves
        /// </summary>
        private void CheckPlayerChanges()
        {
            try
            {
                if (MyAPIGateway.Players == null)
                    return;

                var currentPlayers = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(currentPlayers);

                var currentSteamIds = new HashSet<ulong>();
                foreach (var player in currentPlayers)
                {
                    currentSteamIds.Add(player.SteamUserId);

                    // Check for new players
                    if (!_knownPlayers.Contains(player.SteamUserId))
                    {
                        _knownPlayers.Add(player.SteamUserId);
                        LoggerUtil.LogInfo(
                            $"Player joined: {player.DisplayName} ({player.SteamUserId})"
                        );

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerJoinAsync(
                                player.DisplayName,
                                (long)player.SteamUserId
                            );
                        });
                    }
                }

                // Check for disconnected players
                var disconnected = new List<ulong>();
                foreach (var steamId in _knownPlayers)
                {
                    if (!currentSteamIds.Contains(steamId))
                    {
                        disconnected.Add(steamId);
                    }
                }

                foreach (var steamId in disconnected)
                {
                    _knownPlayers.Remove(steamId);
                    _trackedCharacters.Remove(steamId);

                    string playerName = steamId.ToString();

                    LoggerUtil.LogInfo($"Player left: {playerName} ({steamId})");

                    Task.Run(async () =>
                    {
                        await _eventLog.LogPlayerLeaveAsync(playerName, (long)steamId);
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error checking player changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Hook death events for any newly joined players
        /// </summary>
        private void HookNewPlayers()
        {
            try
            {
                if (MyAPIGateway.Players == null)
                    return;

                var allPlayers = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(allPlayers);

                if (allPlayers == null)
                    return;

                foreach (var player in allPlayers)
                {
                    try
                    {
                        if (player == null || player.Character == null)
                            continue;

                        if (!_trackedCharacters.ContainsKey(player.SteamUserId))
                        {
                            HookCharacterDeath(player.Character, player.SteamUserId);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogDebug($"Error hooking new player: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error in HookNewPlayers: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when character dies (event handler)
        /// </summary>
        private void OnCharacterDied(IMyCharacter deadCharacter, ulong steamId)
        {
            try
            {
                if (deadCharacter == null)
                    return;

                var controller = MyAPIGateway.Players.GetPlayerControllingEntity(deadCharacter);
                if (controller == null)
                    return;

                ulong victimSteamId = controller.SteamUserId;
                string victimName = controller.DisplayName ?? "Unknown";

                LoggerUtil.LogInfo($"[DEATH EVENT] {victimName} ({victimSteamId}) died");

                string deathKey = $"{victimSteamId}:{DateTime.UtcNow:yyyyMMddHHmmss}";
                if (_processedDeaths.Contains(deathKey))
                {
                    LoggerUtil.LogDebug("Duplicate death event suppressed");
                    return;
                }
                _processedDeaths.Add(deathKey);

                // Basic death message (no killer/weapon until we parse system chat)
                string deathMessage = $"{victimName} died";

                // Log to database
                if (_deathLog != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _deathLog.LogPlayerDeathAsync(
                                "Unknown", // killer
                                victimName,
                                "Unknown", // weapon
                                0, // killerId
                                (long)victimSteamId,
                                deadCharacter.GetPosition().ToString() // location
                            );
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError($"Error logging death: {ex.Message}");
                        }
                    });
                }

                // Send to Discord (via EventLoggingService)
                if (_eventLog != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _eventLog.LogDeathAsync(deathMessage);
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError($"Error sending to Discord: {ex.Message}");
                        }
                    });
                }

                // FIXED: Broadcast death to global chat in game
                try
                {
                    MyVisualScriptLogicProvider.SendChatMessage(deathMessage, "Server", 0, "Red");
                    LoggerUtil.LogInfo($"[DEATH CHAT] Broadcasted to game: {deathMessage}");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Failed to broadcast death to game chat: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnCharacterDied: {ex.Message}");
                LoggerUtil.LogDebug($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Process system chat messages (legacy fallback for join/leave and death parsing)
        /// This method is called from plugin when channel == "System"
        /// </summary>
        public void ProcessSystemChatMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                LoggerUtil.LogDebug($"System chat message received: {message}");

                // Check for death messages and forward them
                if (
                    message.Contains(" died")
                    || message.Contains(" was killed")
                    || message.Contains(" suffocated")
                    || message.Contains(" didn't survive")
                )
                {
                    LoggerUtil.LogDebug($"Death message detected in system chat: {message}");

                    // Use the system message directly as death info
                    string deathMessage = message.Trim();

                    // Send to Discord
                    if (_eventLog != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await _eventLog.LogDeathAsync(deathMessage);
                            }
                            catch (Exception ex)
                            {
                                LoggerUtil.LogError($"Error logging chat death: {ex.Message}");
                            }
                        });
                    }

                    // Broadcast to global chat in game (enhanced visibility)
                    try
                    {
                        MyVisualScriptLogicProvider.SendChatMessage(
                            deathMessage,
                            "Server",
                            0,
                            "Red"
                        );
                        LoggerUtil.LogInfo(
                            $"[DEATH CHAT] Broadcasted from system message: {deathMessage}"
                        );
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            $"Failed to broadcast system death message to game chat: {ex.Message}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error processing system chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup - unhook all events
        /// </summary>
        public void Dispose()
        {
            try
            {
                LoggerUtil.LogInfo("Disposing PlayerTrackingService...");

                // Stop polling timer
                if (_pollingTimer != null)
                {
                    _pollingTimer.Stop();
                    _pollingTimer.Dispose();
                }

                // Clear collections
                lock (_lockObject)
                {
                    _trackedCharacters.Clear();
                    _knownPlayers.Clear();
                    _processedDeaths.Clear();
                }

                LoggerUtil.LogSuccess("PlayerTrackingService disposed");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error during disposal: {ex.Message}");
            }
        }
    }
}
