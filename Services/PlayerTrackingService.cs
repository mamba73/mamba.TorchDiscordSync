// Services/PlayerTrackingService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Scripting;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using mamba.TorchDiscordSync.Plugin;
using Sandbox.Game;
using System.Text.RegularExpressions;
using NLog.Fluent;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service responsible for tracking player connections, disconnections, and deaths.
    /// Integrates with Torch chat system and SE damage system for comprehensive event tracking.
    /// </summary>
    public class PlayerTrackingService
    {
        private readonly EventLoggingService _eventLog;
        private readonly ITorchBase _torch;
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
            ITorchBase torch,
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
        /// Hooks into character death events, damage system, chat messages, and player join/leave events.
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
                // Hook player join/leave events
                MyVisualScriptLogicProvider.PlayerConnected += OnPlayerJoined;
                MyVisualScriptLogicProvider.PlayerDisconnected += OnPlayerLeft;
                LoggerUtil.LogInfo("Registered player join/leave handlers");

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
                    var torchInstance = _torch as Torch.API.ITorchServer;
                    var chatManager = torchInstance?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
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
                    LoggerUtil.LogError("Error hooking chat manager: " + ex.Message);
                }

                _isInitialized = true;
                LoggerUtil.LogInfo("PlayerTrackingService initialized successfully");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error initializing PlayerTrackingService: " + ex.Message);
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
                // Unhook player events
                MyVisualScriptLogicProvider.PlayerConnected -= OnPlayerJoined;
                MyVisualScriptLogicProvider.PlayerDisconnected -= OnPlayerLeft;
                MyEntities.OnEntityAdd -= OnEntityAdded;

                // Unhook chat manager
                try
                {
                    var torchInstance = _torch as Torch.API.ITorchServer;
                    var chatManager = torchInstance?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
                    if (chatManager != null)
                    {
                        chatManager.MessageRecieved -= OnChatMessageReceived;
                        LoggerUtil.LogInfo("Unregistered Torch chat message handler");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Error unhooking chat manager: " + ex.Message);
                }

                _lastDamageInfo.Clear();
                _isInitialized = false;
                LoggerUtil.LogInfo("PlayerTrackingService disposed");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error disposing PlayerTrackingService: " + ex.Message);
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
                    LoggerUtil.LogDebug("TorchChatMessage dump:");
                    LoggerUtil.LogDebug("  Author: " + (msg.Author ?? "null"));
                    LoggerUtil.LogDebug("  AuthorSteamId: " + (msg.AuthorSteamId.HasValue ? msg.AuthorSteamId.Value.ToString() : "null"));
                    LoggerUtil.LogDebug("  Message: " + (msg.Message ?? "null"));
                    LoggerUtil.LogDebug("  Timestamp: " + msg.Timestamp.ToString());
                    LoggerUtil.LogDebug("  Color: " + (msg.Color != null ? msg.Color.ToString() : "null"));
                    LoggerUtil.LogDebug("  Chat message channel: " + msg.Channel.ToString());

                    _plugin.ProcessChatMessage(
                        msg.Message,                    // string message
                        msg.Author,                     // string playerName  
                        msg.Channel.ToString()          // string channel
                    );
                    LoggerUtil.LogDebug("Raw chat msg: Author=" + msg.Author + ", SteamId=" + (msg.AuthorSteamId.HasValue ? msg.AuthorSteamId.Value.ToString() : "null") + ", Message=" + msg.Message);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error processing chat message: " + ex.Message);
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
                    _lastDamageInfo[characterId] = info;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error in OnDamageApplied: " + ex.Message);
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
                LoggerUtil.LogError("Error in OnEntityAdded: " + ex.Message);
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
                var myCharacter = character as MyCharacter;
                if (myCharacter == null)
                {
                    LoggerUtil.LogWarning("Cannot cast IMyCharacter to MyCharacter");
                    return;
                }

                long victimIdentityId = myCharacter.GetPlayerIdentityId();
                if (victimIdentityId == 0)
                {
                    LoggerUtil.LogWarning("Cannot get player identity from character");
                    return;
                }

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
                            LoggerUtil.LogDebug("PvP death: " + victimName + " killed by " + killerName + " using " + weaponType);
                        }
                        else if (attackerEntity != null)
                        {
                            // Death caused by a grid or other entity
                            killerName = attackerEntity.DisplayName ?? "Grid";
                            LoggerUtil.LogDebug("Grid/Entity death: " + victimName + " killed by " + killerName);
                        }
                    }
                    else
                    {
                        // No attacker - likely environmental death
                        killerName = "Environment";
                        LoggerUtil.LogDebug("Environmental death: " + victimName + " - " + weaponType);
                    }

                    // Clean up damage info for this character
                    _lastDamageInfo.Remove(character.EntityId);
                }
                else
                {
                    LoggerUtil.LogWarning("No damage info found for character " + victimName + " (EntityId: " + character.EntityId + ")");
                }

                // Get character position for location tracking
                var position = character.GetPosition();
                string location = "X:" + ((int)position.X).ToString() + " Y:" + ((int)position.Y).ToString() + " Z:" + ((int)position.Z).ToString();

                Task.Run(async () =>
                {
                    await _deathLog.LogPlayerDeathAsync(
                        killerName,         // 1. string killerName
                        victimName,         // 2. string victimName
                        weaponType,         // 3. string weaponType
                        killerId,           // 4. long killerId
                        victimIdentityId,   // 5. long victimId
                        location            // 6. string location
                    );
                });

                LoggerUtil.LogInfo("Death logged: " + victimName + " | Killer: " + killerName + " | Weapon: " + weaponType);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error in OnCharacterDied: " + ex.Message);
            }
        }

        /// <summary>
        /// Processes system chat messages to detect special events.
        /// Called from Plugin.ProcessChatMessage for system channel messages.
        /// </summary>
        /// <param name="message">System message text</param>
        public void ProcessSystemMessage(string message)
        {
            try
            {
                LoggerUtil.LogDebug("Processing system message: " + message);
                // System messages are already handled by PlayerConnected/Disconnected events
                // This is a fallback for any other system messages
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error processing system message: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles player join events.
        /// Logs to Discord and broadcasts to game chat using configurable messages.
        /// </summary>
        /// <param name="playerId">Player identity ID</param>
        private async void OnPlayerJoined(long playerId)
        {
            try
            {
                var identity = MySession.Static.Players.TryGetIdentity(playerId);
                if (identity == null) return;

                string playerName = identity.DisplayName;
                ulong steamId = MyAPIGateway.Players.TryGetSteamId(playerId);

                LoggerUtil.LogInfo("Player joined: " + playerName + " (ID: " + playerId + ")");

                string joinTemplate = null;
                if (_plugin.Config != null && _plugin.Config.Chat != null && _plugin.Config.Chat.JoinMessage != null)
                {
                    joinTemplate = _plugin.Config.Chat.JoinMessage;
                }

                string joinMessage = null;
                if (joinTemplate != null)
                {
                    joinMessage = joinTemplate.Replace("{p}", playerName);
                }

                if (joinMessage != null)
                {
                    // Strip emojis only for in-game if option is enabled
                    if (_plugin.Config.Chat.StripEmojisForInGameChat)
                    {
                        joinMessage = RemoveEmojis(joinMessage);
                        LoggerUtil.LogInfo("Message after emoji removal: " + joinMessage);
                    }
                    MyVisualScriptLogicProvider.SendChatMessage(
                        joinMessage,
                        "Server",
                        0,
                        "Green"
                    );
                }

                if (_eventLog != null)
                {
                    await _eventLog.LogPlayerJoinAsync(playerName, 0UL);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error in OnPlayerJoined: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles player leave events.
        /// Logs to Discord and broadcasts to game chat using configurable messages.
        /// </summary>
        /// <param name="playerId">Player identity ID</param>
        private async void OnPlayerLeft(long playerId)
        {
            try
            {
                var identity = MySession.Static.Players.TryGetIdentity(playerId);
                if (identity == null) return;

                string playerName = identity.DisplayName;
                ulong steamId = MyAPIGateway.Players.TryGetSteamId(playerId);

                LoggerUtil.LogInfo("Player left: " + playerName + " (ID: " + playerId + ")");

                string leaveTemplate = null;
                if (_plugin.Config != null && _plugin.Config.Chat != null && _plugin.Config.Chat.LeaveMessage != null)
                {
                    leaveTemplate = _plugin.Config.Chat.LeaveMessage;
                }

                string leaveMessage = null;
                if (leaveTemplate != null)
                {
                    leaveMessage = leaveTemplate.Replace("{p}", playerName);
                }

                if (leaveMessage != null)
                {
                    // Strip emojis only for in-game if option is enabled
                    if (_plugin.Config.Chat.StripEmojisForInGameChat)
                    {
                        leaveMessage = RemoveEmojis(leaveMessage);
                        LoggerUtil.LogInfo("Message after emoji removal: " + leaveMessage);
                    }

                    MyVisualScriptLogicProvider.SendChatMessage(
                        leaveMessage,
                        "Server",
                        0,
                        "Red"
                    );

                    if (_eventLog != null)
                    {
                        await _eventLog.LogPlayerLeaveAsync(playerName, 0UL);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error in OnPlayerLeft: " + ex.Message);
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
                LoggerUtil.LogError("Error getting online player count: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Removes emojis and common emoji shortcuts (like :sunny:) from the string.
        /// Logs original and cleaned message.
        /// </summary>
        public static string RemoveEmojis(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            LoggerUtil.LogDebug("Original message before emoji removal: " + input);

            // Korak 1: Ukloni Discord shortcode-ove :ime: (sa ili bez razmaka oko njih)
            string cleaned = System.Text.RegularExpressions.Regex.Replace(input, @"\s*:[a-zA-Z0-9_+-]+:\s*", " ");

            // Korak 2: Ukloni preostale Unicode emojije (ako ih ima)
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^\x00-\x7F]+", " ");

            // Korak 3: Zamijeni višak razmaka sa jednim i trim
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            LoggerUtil.LogDebug("Message after emoji removal: " + cleaned);

            return cleaned;
        }
    }
}