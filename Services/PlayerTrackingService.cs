// Services/PlayerTrackingService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game; // For MyVisualScriptLogicProvider
using Sandbox.ModAPI;
using Torch.API;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for tracking player joins/leaves and DEATHS via IMyCharacter.CharacterDied event
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        private readonly ITorchBase _torch;
        private readonly DeathLogService _deathLog;

        // Cache player names for join/leave messages (prevents SteamID display on leave)
        private Dictionary<ulong, string> _playerNames = new Dictionary<ulong, string>();

        private System.Timers.Timer _pollingTimer;
        private HashSet<ulong> _knownPlayers = new HashSet<ulong>();
        private Dictionary<ulong, IMyCharacter> _trackedCharacters =
            new Dictionary<ulong, IMyCharacter>();
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

        public void Initialize()
        {
            try
            {
                LoggerUtil.LogDebug(
                    "Initializing PlayerTrackingService with CharacterDied event hooking..."
                );
                InitializePolling();
                InitializeDeathTracking();
                LoggerUtil.LogSuccess("Player tracking initialized (event-based death detection)");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Player tracking initialization failed: " + ex.Message);
            }
        }

        private void InitializePolling()
        {
            _pollingTimer = new System.Timers.Timer(5000);
            _pollingTimer.Elapsed += OnPollingTick;
            _pollingTimer.AutoReset = true;
            _pollingTimer.Start();
            LoggerUtil.LogInfo("Player polling timer started (5-second intervals)");
        }

        private void InitializeDeathTracking()
        {
            if (MyAPIGateway.Players == null)
                return;

            var allPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(allPlayers);

            foreach (var player in allPlayers)
            {
                if (player?.Character == null)
                    continue;

                HookCharacterDeath(player.Character, player.SteamUserId);
                _knownPlayers.Add(player.SteamUserId);
                _playerNames[player.SteamUserId] = player.DisplayName;
            }

            LoggerUtil.LogInfo($"Death tracking hooked for {_knownPlayers.Count} players");
        }

        private void HookCharacterDeath(IMyCharacter character, ulong steamId)
        {
            if (character == null || _trackedCharacters.ContainsKey(steamId))
                return;

            character.CharacterDied += deadChar => OnCharacterDied(deadChar, steamId);
            _trackedCharacters[steamId] = character;
            LoggerUtil.LogDebug($"Hooked CharacterDied for SteamID {steamId}");
        }

        private void OnPollingTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckPlayerChanges();
            HookNewPlayers();
        }

        private void CheckPlayerChanges()
        {
            if (MyAPIGateway.Players == null)
                return;

            var currentPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(currentPlayers);

            var currentSteamIds = new HashSet<ulong>();
            foreach (var player in currentPlayers)
            {
                currentSteamIds.Add(player.SteamUserId);

                if (!_knownPlayers.Contains(player.SteamUserId))
                {
                    _knownPlayers.Add(player.SteamUserId);
                    _playerNames[player.SteamUserId] = player.DisplayName;
                    LoggerUtil.LogInfo(
                        $"Player joined: {player.DisplayName} ({player.SteamUserId})"
                    );
                    _ = _eventLog.LogPlayerJoinAsync(player.DisplayName, player.SteamUserId);
                }
            }

            var disconnected = new List<ulong>();
            foreach (var steamId in _knownPlayers)
            {
                if (!currentSteamIds.Contains(steamId))
                    disconnected.Add(steamId);
            }

            foreach (var steamId in disconnected)
            {
                _knownPlayers.Remove(steamId);
                _trackedCharacters.Remove(steamId);

                string playerName = _playerNames.TryGetValue(steamId, out var name)
                    ? name
                    : steamId.ToString();
                _playerNames.Remove(steamId);

                LoggerUtil.LogInfo($"Player left: {playerName} ({steamId})");
                _ = _eventLog.LogPlayerLeaveAsync(playerName, steamId);
            }
        }

        private void HookNewPlayers()
        {
            if (MyAPIGateway.Players == null)
                return;

            var allPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(allPlayers);

            foreach (var player in allPlayers)
            {
                if (player?.Character == null)
                    continue;

                if (!_trackedCharacters.ContainsKey(player.SteamUserId))
                {
                    HookCharacterDeath(player.Character, player.SteamUserId);
                    _playerNames[player.SteamUserId] = player.DisplayName;
                }
            }
        }

        private void OnCharacterDied(IMyCharacter deadCharacter, ulong steamId)
        {
            try
            {
                if (deadCharacter == null)
                    return;

                // Skip if already dead (prevents duplicate triggers from SE)
                if (deadCharacter.IsDead)
                    return;

                var controller = MyAPIGateway.Players.GetPlayerControllingEntity(deadCharacter);
                if (controller == null)
                    return;

                ulong victimSteamId = controller.SteamUserId;
                string victimName = controller.DisplayName ?? "Unknown";

                LoggerUtil.LogInfo($"[DEATH EVENT] {victimName} ({victimSteamId}) died");

                // Improved death key with milliseconds to handle multiple deaths in the same second
                string deathKey = $"{victimSteamId}:{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                if (_processedDeaths.Contains(deathKey))
                    return;

                _processedDeaths.Add(deathKey);

                string killerName = "Unknown";
                long killerId = 0;
                string weapon = "Unknown";
                string location = deadCharacter.GetPosition().ToString();

                // Log to database
                if (_deathLog != null)
                {
                    _ = _deathLog.LogPlayerDeathAsync(
                        killerName,
                        victimName,
                        weapon,
                        killerId,
                        (long)victimSteamId,
                        location,
                        deadCharacter.DisplayName ?? victimName
                    );
                }

                // Generate clean message WITHOUT coordinates for in-game chat
                string gameMsg =
                    killerName == "Unknown"
                        ? $"{victimName} died"
                        : $"{killerName} killed {victimName} with {weapon}";

                // Optional: detailed message for Discord (with location if needed)
                string discordMsg = gameMsg + $" at {location}";

                // Send to global chat in game (clean version, no coordinates)
                try
                {
                    MyVisualScriptLogicProvider.SendChatMessage(gameMsg, "Server", 0, "Red");
                    LoggerUtil.LogInfo($"[DEATH CHAT] Broadcasted to game: {gameMsg}");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Failed to broadcast death to game chat: {ex.Message}");
                }

                // Send to Discord ONLY if needed (commented out to prevent duplication - use ProcessSystemChatMessage or DeathLogService instead)
                // if (_eventLog != null)
                // {
                //     _ = _eventLog.LogDeathAsync(discordMsg);
                // }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnCharacterDied: {ex.Message}");
            }
        }

        public void ProcessSystemChatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (message.Contains(" died") || message.Contains(" was killed"))
            {
                _ = _eventLog.LogDeathAsync(message);
            }
        }

        public void Dispose()
        {
            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Dispose();
            }

            lock (_lockObject)
            {
                _trackedCharacters.Clear();
                _knownPlayers.Clear();
                _processedDeaths.Clear();
                _playerNames.Clear();
            }
        }
    }
}
