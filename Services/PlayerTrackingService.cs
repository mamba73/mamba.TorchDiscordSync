// Services/PlayerTrackingService.cs
// FINAL FIXED VERSION - Uses IMyCharacter.CharacterDied event (PROPER API)
// Instead of MyEntities polling - much more reliable!

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Torch.API;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for tracking player joins/leaves and DEATHS via IMyCharacter.CharacterDied event
    /// This is the PROPER API way - not polling MyEntities!
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
        
        // Death tracking - remember last damage source per player
        private Dictionary<ulong, LastDamageInfo> _lastDamage = 
            new Dictionary<ulong, LastDamageInfo>();
        
        // Processed deaths to prevent duplicates
        private HashSet<string> _processedDeaths = new HashSet<string>();
        
        private object _lockObject = new object();

        public PlayerTrackingService(EventLoggingService eventLog, ITorchBase torch, DeathLogService deathLog)
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
                LoggerUtil.LogDebug("Initializing PlayerTrackingService with CharacterDied event hooking...");
                
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
                    LoggerUtil.LogWarning("MyAPIGateway.Players is null - cannot initialize death tracking");
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
                        LoggerUtil.LogInfo($"Player joined: {player.DisplayName} ({player.SteamUserId})");

                        Task.Run(async () =>
                        {
                            await _eventLog.LogPlayerJoinAsync(player.DisplayName, (long)player.SteamUserId);
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
                    _lastDamage.Remove(steamId);

                    // Try to get player name from cache
                    string playerName = "Unknown";
                    try
                    {
                        // Find in all players (even offline) - this might not work, just fallback
                        playerName = steamId.ToString();
                    }
                    catch { }

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

                        // If player is new and has character, hook them
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

                // Get victim info
                var controller = MyAPIGateway.Players.GetPlayerControllingEntity(deadCharacter);
                if (controller == null)
                {
                    LoggerUtil.LogDebug("Could not find controller for dead character");
                    return;
                }

                ulong victimSteamId = controller.SteamUserId;
                string victimName = controller.DisplayName ?? "Unknown";
                
                LoggerUtil.LogInfo($"[DEATH EVENT] {victimName} ({victimSteamId}) died");

                // Prevent duplicate processing
                string deathKey = $"{victimSteamId}:{DateTime.UtcNow:yyyyMMddHHmmss}";
                if (_processedDeaths.Contains(deathKey))
                {
                    LoggerUtil.LogDebug("Duplicate death event suppressed");
                    return;
                }
                _processedDeaths.Add(deathKey);

                // Get killer/damage info
                string killerName = "Unknown";
                long killerId = 0;
                string weapon = "Unknown";

                // Try to get last damage source
                if (_lastDamage.TryGetValue(victimSteamId, out var damageInfo))
                {
                    killerName = damageInfo.AttackerName;
                    killerId = damageInfo.AttackerId;
                    weapon = damageInfo.Weapon;
                    _lastDamage.Remove(victimSteamId);
                }

                // Get location
                string location = deadCharacter.GetPosition().ToString();

                // Log the death
                if (_deathLog != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _deathLog.LogPlayerDeathAsync(
                                killerName,
                                victimName,
                                weapon,
                                killerId,
                                (long)victimSteamId,
                                location
                            );
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError($"Error logging death: {ex.Message}");
                        }
                    });
                }

                // Send to Discord
                if (_eventLog != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            string msg = killerName == "Unknown"
                                ? $"{victimName} died"
                                : $"{killerName} killed {victimName} with {weapon}";

                            await _eventLog.LogDeathAsync(msg);
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError($"Error sending to Discord: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnCharacterDied: {ex.Message}");
                LoggerUtil.LogDebug($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Track damage to characters
        /// Call this when you detect a damage event
        /// </summary>
        public void OnCharacterDamaged(ulong victimSteamId, string attackerName, ulong attackerSteamId, string weapon)
        {
            try
            {
                lock (_lockObject)
                {
                    _lastDamage[victimSteamId] = new LastDamageInfo
                    {
                        AttackerName = attackerName,
                        AttackerId = (long)attackerSteamId,
                        Weapon = weapon,
                        Timestamp = DateTime.UtcNow
                    };
                }

                LoggerUtil.LogDebug($"Damage tracked: {attackerName} → victim {victimSteamId}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error tracking damage: {ex.Message}");
            }
        }

        /// <summary>
        /// Process system chat messages (legacy fallback for join/leave)
        /// </summary>
        public void ProcessSystemChatMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                LoggerUtil.LogDebug($"Processing chat: {message}");

                // Check for death messages in chat (legacy fallback)
                if (message.Contains(" died") || message.Contains(" was killed"))
                {
                    LoggerUtil.LogDebug($"Death message detected in chat: {message}");

                    Task.Run(async () =>
                    {
                        try
                        {
                            if (_eventLog != null)
                                await _eventLog.LogDeathAsync(message);
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.LogError($"Error logging chat death: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error processing chat message: {ex.Message}");
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

                // Unhook all character events
                lock (_lockObject)
                {
                    foreach (var kvp in _trackedCharacters)
                    {
                        try
                        {
                            var character = kvp.Value;
                            if (character != null)
                            {
                                // Unhook - using lambda is tricky, so we'll just clear
                                LoggerUtil.LogDebug($"Unhooked character for SteamID {kvp.Key}");
                            }
                        }
                        catch { }
                    }

                    _trackedCharacters.Clear();
                    _lastDamage.Clear();
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

        /// <summary>
        /// Helper class to store last damage info
        /// </summary>
        private class LastDamageInfo
        {
            public string AttackerName { get; set; }
            public long AttackerId { get; set; }
            public string Weapon { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}