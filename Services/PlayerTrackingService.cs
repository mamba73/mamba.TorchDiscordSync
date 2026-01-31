// Services/PlayerTrackingService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API;
using Torch.Managers.ChatManager;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service responsible for tracking player connections, disconnections, and deaths.
    /// Integrates with Torch chat system and SE damage system for comprehensive event tracking.
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly DatabaseService _databaseService;
        private readonly DeathLogService _deathLogService;
        private readonly MambaTorchDiscordSyncPlugin _plugin;
        private readonly Dictionary<long, MyDamageInformation> _lastDamageInfo;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the PlayerTrackingService.
        /// </summary>
        /// <param name="databaseService">Database service for storing player data</param>
        /// <param name="deathLogService">Death log service for analyzing death types</param>
        /// <param name="plugin">Main plugin instance for chat message forwarding</param>
        public PlayerTrackingService(
            DatabaseService databaseService, 
            DeathLogService deathLogService,
            MambaTorchDiscordSyncPlugin plugin)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _deathLogService = deathLogService ?? throw new ArgumentNullException(nameof(deathLogService));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _lastDamageInfo = new Dictionary<long, MyDamageInformation>();
        }

        /// <summary>
        /// Initializes the player tracking service by subscribing to game events.
        /// Hooks into character death events, damage system, and chat messages.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                LoggerUtil.LogWarning("PlayerTrackingService already initialized");
                return;
            }

            try
            {
                // Hook character death event
                MyEntities.OnEntityAdd += OnEntityAdded;
                
                // Hook damage system for death detection
                if (MyAPIGateway.Session?.DamageSystem != null)
                {
                    MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(
                        priority: 100,
                        handler: OnDamageApplied
                    );
                    LoggerUtil.LogInfo("Registered damage handler for death tracking");
                }
                else
                {
                    LoggerUtil.LogWarning("DamageSystem is null - damage tracking disabled");
                }

                // Hook Torch chat manager for chat integration
                try
                {
                    var chatManager = TorchBase.Instance?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
                    if (chatManager != null)
                    {
                        chatManager.MessageRecieved += OnChatMessageReceived;
                        LoggerUtil.LogInfo("Registered Torch chat message handler");
                    }
                    else
                    {
                        LoggerUtil.LogWarning("ChatManagerServer is null - chat integration disabled");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Error hooking chat manager: {ex.Message}");
                }

                _isInitialized = true;
                LoggerUtil.LogInfo("PlayerTrackingService initialized successfully");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error initializing PlayerTrackingService: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unsubscribes from all game events and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                MyEntities.OnEntityAdd -= OnEntityAdded;

                // Unhook chat manager
                try
                {
                    var chatManager = TorchBase.Instance?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
                    if (chatManager != null)
                    {
                        chatManager.MessageRecieved -= OnChatMessageReceived;
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Error unhooking chat manager: {ex.Message}");
                }

                _lastDamageInfo.Clear();
                _isInitialized = false;

                LoggerUtil.LogInfo("PlayerTrackingService disposed");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error disposing PlayerTrackingService: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles chat messages received from Torch chat manager.
        /// Forwards player chat messages to Discord via the main plugin.
        /// </summary>
        /// <param name="msg">The chat message data</param>
        /// <param name="consumed">Reference to consumed flag (not modified)</param>
        private void OnChatMessageReceived(TorchChatMessage msg, ref bool consumed)
        {
            try
            {
                // Only process messages from actual players (with valid SteamID)
                if (msg.AuthorSteamId.HasValue && msg.AuthorSteamId.Value != 0)
                {
                    _plugin.ProcessChatMessage(
                        msg.AuthorSteamId.Value,  // ulong
                        msg.Author,                // string
                        msg.Message,               // string
                        false                      // isSystemMessage
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error processing chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores damage information for later use in death detection.
        /// Called after damage is applied to any entity.
        /// </summary>
        /// <param name="target">The entity that received damage</param>
        /// <param name="info">Damage information including attacker and damage type</param>
        private void OnDamageApplied(object target, MyDamageInformation info)
        {
            try
            {
                if (target is IMyCharacter character)
                {
                    long characterId = character.EntityId;
                    
                    // Store the latest damage info for this character
                    // This will be used in OnCharacterDied to determine cause of death
                    _lastDamageInfo[characterId] = info;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnDamageApplied: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a new entity is added to the game world.
        /// Subscribes to character death events for player characters.
        /// </summary>
        /// <param name="entity">The entity being added</param>
        private void OnEntityAdded(VRage.Game.Entity.MyEntity entity)
        {
            try
            {
                var character = entity as MyCharacter;
                if (character != null)
                {
                    // Subscribe to character died event
                    character.CharacterDied += OnCharacterDied;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnEntityAdded: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles player connection events (currently not implemented).
        /// </summary>
        /// <param name="steamId">Steam ID of the connecting player</param>
        /// <param name="playerName">Display name of the connecting player</param>
        public void OnPlayerConnected(ulong steamId, string playerName)
        {
            try
            {
                LoggerUtil.LogInfo($"Player connected: {playerName} (SteamID: {steamId})");
                
                // Store player data
                _databaseService.SavePlayer(new PlayerModel
                {
                    SteamID = (long)steamId,
                    PlayerName = playerName,
                    LastSeen = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnPlayerConnected: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles player disconnection events (currently not implemented).
        /// </summary>
        /// <param name="steamId">Steam ID of the disconnecting player</param>
        /// <param name="playerName">Display name of the disconnecting player</param>
        public void OnPlayerDisconnected(ulong steamId, string playerName)
        {
            try
            {
                LoggerUtil.LogInfo($"Player disconnected: {playerName} (SteamID: {steamId})");
                
                // Update last seen timestamp
                var player = _databaseService.GetPlayerBySteamID((long)steamId);
                if (player != null)
                {
                    player.LastSeen = DateTime.UtcNow;
                    _databaseService.SavePlayer(player);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnPlayerDisconnected: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles character death events.
        /// Analyzes damage information to determine cause of death and killer.
        /// </summary>
        /// <param name="character">The character that died</param>
        private void OnCharacterDied(IMyCharacter character)
        {
            try
            {
                if (character == null)
                {
                    LoggerUtil.LogWarning("OnCharacterDied called with null character");
                    return;
                }

                // Get victim information
                long victimIdentityId = MyAPIGateway.Players.TryGetIdentityId(character.ControlSteamId);
                string victimName = character.DisplayName ?? "Unknown";
                
                // Initialize death data
                long killerId = 0;
                string killerName = "Unknown";
                string weaponType = "Unknown";
                
                // Try to get damage information for this character
                if (_lastDamageInfo.TryGetValue(character.EntityId, out MyDamageInformation damageInfo))
                {
                    killerId = damageInfo.AttackerId;
                    weaponType = damageInfo.Type.String; // MyStringHash.String property
                    
                    // If there's an attacker, try to identify them
                    if (killerId != 0)
                    {
                        var attackerEntity = MyAPIGateway.Entities.GetEntityById(killerId);
                        
                        if (attackerEntity is IMyCharacter attackerChar)
                        {
                            // Death caused by another player
                            killerName = attackerChar.DisplayName ?? "Unknown";
                            LoggerUtil.LogDebug($"PvP death: {victimName} killed by {killerName} using {weaponType}");
                        }
                        else if (attackerEntity != null)
                        {
                            // Death caused by a grid or other entity
                            killerName = attackerEntity.DisplayName ?? attackerEntity.GetType().Name;
                            LoggerUtil.LogDebug($"Grid/Entity death: {victimName} killed by {killerName}");
                        }
                    }
                    else
                    {
                        // No attacker - likely environmental death
                        LoggerUtil.LogDebug($"Environmental death: {victimName} - {weaponType}");
                    }
                    
                    // Clean up damage info for this character
                    _lastDamageInfo.Remove(character.EntityId);
                }
                else
                {
                    LoggerUtil.LogWarning($"No damage info found for character {victimName} (EntityId: {character.EntityId})");
                }
                
                // Get character position for location tracking
                var position = character.GetPosition();
                string location = $"X:{(int)position.X} Y:{(int)position.Y} Z:{(int)position.Z}";
                
                // Log the death through DeathLogService
                _deathLogService.LogDeath(
                    victimIdentityId,
                    victimName,
                    killerName,
                    weaponType,
                    killerId,
                    location
                );
                
                LoggerUtil.LogInfo($"Death logged: {victimName} | Killer: {killerName} | Weapon: {weaponType}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in OnCharacterDied: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current online player count.
        /// </summary>
        /// <returns>Number of online players</returns>
        public int GetOnlinePlayerCount()
        {
            try
            {
                if (MySession.Static?.Players == null)
                    return 0;

                return MySession.Static.Players.GetOnlinePlayers().Count;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error getting online player count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets all currently online players.
        /// </summary>
        /// <returns>List of online player models</returns>
        public List<PlayerModel> GetOnlinePlayers()
        {
            var players = new List<PlayerModel>();

            try
            {
                if (MySession.Static?.Players == null)
                    return players;

                var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
                
                foreach (var player in onlinePlayers)
                {
                    try
                    {
                        ulong steamId = player.SteamUserId;
                        if (steamId == 0) continue;

                        players.Add(new PlayerModel
                        {
                            SteamID = (long)steamId,
                            PlayerName = player.DisplayName ?? "Unknown",
                            LastSeen = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError($"Error processing player: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error getting online players: {ex.Message}");
            }

            return players;
        }
    }
}
