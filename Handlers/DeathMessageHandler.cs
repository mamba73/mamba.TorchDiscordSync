// Handlers/DeathMessageHandler.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Handlers
{
    /// <summary>
    /// ENHANCED: Handles death message generation with:
    /// - Killer detection (player, turret, NPC faction)
    /// - Location zones (planet, sector, deep space)
    /// - Random message templates
    /// - Full "who killed whom with what and where" format
    /// </summary>
    public class DeathMessageHandler
    {
        private readonly EventLoggingService _eventLog;
        private readonly MainConfig _config;
        private readonly DeathMessagesConfig _deathMessagesConfig;
        private readonly KillerDetectionService _killerDetector;
        private readonly DeathLocationService _locationService;

        public DeathMessageHandler(EventLoggingService eventLog, MainConfig config)
        {
            _eventLog = eventLog;
            _config = config;
            _deathMessagesConfig = DeathMessagesConfig.Load();
            _killerDetector = new KillerDetectionService();
            _locationService = new DeathLocationService(config);

            LoggerUtil.LogInfo("[DEATH_HANDLER] Initialized with killer detection and location zones");
        }

        /// <summary>
        /// ENHANCED: Process a player death with full context
        /// Detects: killer, weapon, location, and generates appropriate message
        /// </summary>
        public async Task HandlePlayerDeathAsync(string playerName, IMyCharacter character = null)
        {
            try
            {
                LoggerUtil.LogInfo($"[DEATH] ═══ Processing death for: {playerName} ═══");

                // STEP 1: Detect killer (if character provided)
                KillerDetectionService.KillerInfo killerInfo = null;
                if (character != null)
                {
                    killerInfo = _killerDetector.DetectKiller(character);
                    LoggerUtil.LogDebug($"[DEATH] Killer: {killerInfo.KillerName}, Weapon: {killerInfo.WeaponName}, Cause: {killerInfo.Cause}");
                }
                else
                {
                    LoggerUtil.LogWarning("[DEATH] Character is null - cannot detect killer");
                    killerInfo = new KillerDetectionService.KillerInfo();
                }

                // STEP 2: Detect location (if character and zones enabled)
                LocationZoneResult locationInfo = null;
                if (character != null && _config?.Death?.EnableLocationZones == true)
                {
                    locationInfo = _locationService.DetectDeathZone(character);
                    LoggerUtil.LogDebug($"[DEATH] Location: Zone={locationInfo.Zone}, Planet={locationInfo.PlanetName ?? "N/A"}");
                }
                else
                {
                    LoggerUtil.LogDebug("[DEATH] Location detection disabled or character null");
                    locationInfo = new LocationZoneResult();
                }

                // STEP 3: Generate comprehensive death message
                string deathMessage = GenerateDeathMessage(playerName, killerInfo, locationInfo);
                LoggerUtil.LogInfo($"[DEATH] Generated message: {deathMessage}");

                // STEP 4: Send to game chat
                SendToGameChat(deathMessage);

                // STEP 5: Add emoticon and send to Discord
                string discordMessage = AddEmotePrefix(deathMessage);
                await SendToDiscordAsync(discordMessage);

                LoggerUtil.LogSuccess($"[DEATH] ═══ Processing complete for {playerName} ═══");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_HANDLER] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// ENHANCED: Generate death message with killer, weapon, and location
        /// Format: "X killed Y with Z at/near/in W"
        /// </summary>
        private string GenerateDeathMessage(string victimName, KillerDetectionService.KillerInfo killerInfo, LocationZoneResult locationInfo)
        {
            try
            {
                // Determine death type for message template selection
                DeathTypeEnum deathType = DetermineDeathType(killerInfo);

                // Get random message template for this death type
                string template = _deathMessagesConfig.GetRandomMessage(deathType);

                // Generate location text
                string locationText = GenerateLocationText(locationInfo);

                // Build the message with all context
                string message = template;

                // Replace victim
                message = message.Replace("{victim}", victimName);
                message = message.Replace("{1}", victimName);

                // Replace killer
                string killerName = GetKillerDisplayName(killerInfo);
                message = message.Replace("{killer}", killerName);
                message = message.Replace("{0}", killerName);

                // Replace weapon
                string weaponName = GetWeaponDisplayName(killerInfo);
                message = message.Replace("{weapon}", weaponName);
                message = message.Replace("{2}", weaponName);

                // Add location if not already in template
                if (!string.IsNullOrEmpty(locationText) && !message.Contains(locationText))
                {
                    message = $"{message} {locationText}";
                }

                return message;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_MSG_GEN] Error: {ex.Message}");
                return $"{victimName} died";
            }
        }

        /// <summary>
        /// Determines which death type to use for message template selection
        /// </summary>
        private DeathTypeEnum DetermineDeathType(KillerDetectionService.KillerInfo killerInfo)
        {
            if (killerInfo == null)
                return DeathTypeEnum.Accident;

            switch (killerInfo.Cause)
            {
                case KillerDetectionService.DeathCause.Player:
                    return DeathTypeEnum.PvP;

                case KillerDetectionService.DeathCause.Turret:
                case KillerDetectionService.DeathCause.NpcFaction:
                    return DeathTypeEnum.PvP; // Use PvP messages for turret kills too

                case KillerDetectionService.DeathCause.Collision:
                    return DeathTypeEnum.Grid;

                case KillerDetectionService.DeathCause.Fall:
                    return DeathTypeEnum.Environment_Collision;

                case KillerDetectionService.DeathCause.Oxygen:
                    return DeathTypeEnum.Environment_Oxygen;

                case KillerDetectionService.DeathCause.Pressure:
                    return DeathTypeEnum.Environment_Pressure;

                case KillerDetectionService.DeathCause.Suicide:
                    return DeathTypeEnum.Suicide;

                case KillerDetectionService.DeathCause.Environment:
                    return DeathTypeEnum.Environment_Collision;

                default:
                    return DeathTypeEnum.Accident;
            }
        }

        /// <summary>
        /// Gets display name for killer with proper formatting
        /// </summary>
        private string GetKillerDisplayName(KillerDetectionService.KillerInfo killerInfo)
        {
            if (killerInfo == null)
                return "Unknown";

            // NPC faction turret: "SPID's turret" or just "SPID"
            if (killerInfo.IsNpcFaction && !string.IsNullOrEmpty(killerInfo.NpcFactionTag))
            {
                if (killerInfo.IsTurretKill)
                    return $"{killerInfo.NpcFactionTag}'s turret";
                return killerInfo.NpcFactionTag;
            }

            // Player-owned turret: "PlayerName's turret" or just player name
            if (killerInfo.IsTurretKill && !string.IsNullOrEmpty(killerInfo.TurretOwnerName))
            {
                return killerInfo.TurretOwnerName;
            }

            // Direct player kill or other
            if (!string.IsNullOrEmpty(killerInfo.KillerName))
                return killerInfo.KillerName;

            return "Unknown";
        }

        /// <summary>
        /// Gets display name for weapon
        /// </summary>
        private string GetWeaponDisplayName(KillerDetectionService.KillerInfo killerInfo)
        {
            if (killerInfo == null)
                return "Unknown";

            // For turret kills, specify it's a turret
            if (killerInfo.IsTurretKill && !string.IsNullOrEmpty(killerInfo.WeaponName))
            {
                return killerInfo.WeaponName; // Already says "Turret" or specific turret name
            }

            // For weapon kills
            if (!string.IsNullOrEmpty(killerInfo.WeaponName))
                return killerInfo.WeaponName;

            // Environmental deaths don't have weapons
            if (killerInfo.Cause == KillerDetectionService.DeathCause.Oxygen)
                return "asphyxiation";
            if (killerInfo.Cause == KillerDetectionService.DeathCause.Fall)
                return "gravity";
            if (killerInfo.Cause == KillerDetectionService.DeathCause.Collision)
                return "collision";

            return "Unknown";
        }

        /// <summary>
        /// Generates location text from zone result
        /// Example: "near Earth", "in deep space", "orbiting Moon"
        /// </summary>
        private string GenerateLocationText(LocationZoneResult locationInfo)
        {
            if (locationInfo == null || !(_config?.Death?.EnableLocationZones ?? false))
                return "";

            try
            {
                bool showGridName = _config?.Death?.ShowGridName ?? true;
                string locationText = _locationService.GenerateLocationText(locationInfo, showGridName);

                if (!string.IsNullOrEmpty(locationText))
                    return locationText;

                return "";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_LOCATION] Error generating location text: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Send death message to in-game chat
        /// </summary>
        private void SendToGameChat(string deathMessage)
        {
            try
            {
                LoggerUtil.LogDebug($"[DEATH_GAME] Sending to game: {deathMessage}");
                MyVisualScriptLogicProvider.SendChatMessage(deathMessage, "Server", 0, "Red");
                LoggerUtil.LogInfo($"[DEATH_GAME] Broadcasted to game");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_GAME] Failed to broadcast: {ex.Message}");
            }
        }

        /// <summary>
        /// Add random emoticon from configuration
        /// </summary>
        private string AddEmotePrefix(string message)
        {
            try
            {
                if (_config?.Death == null || string.IsNullOrEmpty(_config.Death.DeathMessageEmotes))
                    return $"💀 {message}";

                var emotes = _config.Death.DeathMessageEmotes.Split(',');
                if (emotes.Length == 0)
                    return $"💀 {message}";

                string randomEmote = emotes[new Random().Next(emotes.Length)].Trim();
                return $"{randomEmote} {message}";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_EMOTE] Error: {ex.Message}");
                return $"💀 {message}";
            }
        }

        /// <summary>
        /// Send death message directly to Discord
        /// </summary>
        private async Task SendToDiscordAsync(string discordMessage)
        {
            try
            {
                if (_eventLog == null)
                {
                    LoggerUtil.LogWarning("[DEATH_DISCORD] EventLoggingService is null");
                    return;
                }

                LoggerUtil.LogDebug($"[DEATH_DISCORD] Sending: {discordMessage}");
                await _eventLog.LogDeathAsync(discordMessage);
                LoggerUtil.LogSuccess("[DEATH_DISCORD] Message sent");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_DISCORD] Failed: {ex.Message}");
            }
        }
    }
}