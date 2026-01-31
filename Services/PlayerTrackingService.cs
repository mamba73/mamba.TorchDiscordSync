// Services/PlayerTrackingService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
// using Torch.API;
using Torch;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRage.Game;
using VRage.Game.ModAPI;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using mamba.TorchDiscordSync.Plugin;
using mamba.TorchDiscordSync;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service responsible for tracking player connections, disconnections, and deaths.
    /// Integrates with Torch chat system and SE damage system for comprehensive event tracking.
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        // private readonly ITorchBase _torch;
        private readonly TorchBase _torch;
        private readonly DeathLogService _deathLog;
        private readonly MambaTorchDiscordSyncPlugin _plugin;
        private readonly Dictionary<long, MyDamageInformation> _lastDamageInfo;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the PlayerTrackingService.
        /// </summary>
        /// <param name="eventLog">Event logging service</param>
        /// <param name="torch">Torch base instance</param>
        /// <param name="deathLog">Death log service for analyzing death types</param>
        /// <param name="plugin">Main plugin instance for chat message forwarding</param>
        public PlayerTrackingService(
            EventLoggingService eventLog,
            TorchBase torch,
            DeathLogService deathLog,
            MambaTorchDiscordSyncPlugin plugin)
        {
            _eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
            _torch = torch ?? throw new ArgumentNullException(nameof(torch));
            _deathLog = deathLog ?? throw new ArgumentNullException(nameof(deathLog));
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
                        msg.Message,      // string message
                        msg.Author,       // string playerName  
                        "Global"          // string channel
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
                // long victimIdentityId = character.GetPlayerIdentityId();
                var myChar = character as MyCharacter;
                if (myChar == null)
                    return;

                long victimIdentityId = myChar.ControllerInfo?.ControllingIdentityId ?? 0;

                if (victimIdentityId == 0)
                {
                    LoggerUtil.LogWarning("Cannot get player identity from character");
                    return;
                }

                ulong victimSteamId = MyAPIGateway.Players.TryGetSteamId(victimIdentityId);
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
                _ = _deathLog.LogPlayerDeathAsync(
                    killerName,                    // 1. string – killer
                    victimName,                    // 2. string – žrtva
                    weaponType,                    // 3. string – oružje
                    killerId,                      // 4. long   – SteamID ubojice
                    victimIdentityId,              // 5. long   – IdentityID žrtve (NE ToString()!)
                    location                       // 6. string – lokacija
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
    }
}