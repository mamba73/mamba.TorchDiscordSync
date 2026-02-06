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
    /// FINAL ENHANCED: Complete death message handler with:
    /// - Proximity-based killer detection
    /// - Surface vs orbit detection
    /// - Player name sanitization
    /// - Full contextual messages
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

            LoggerUtil.LogInfo(
                "[DEATH_HANDLER] Initialized with proximity detection and surface/orbit zones"
            );
        }

        /// <summary>
        /// ENHANCED: Process death with full context detection
        /// </summary>
        public async Task HandlePlayerDeathAsync(string playerName, IMyCharacter character = null)
        {
            try
            {
                // CRITICAL: Sanitize player name (remove monitor icons, etc.)
                string sanitizedName = TextSanitizationUtil.SanitizePlayerName(playerName);

                LoggerUtil.LogInfo($"[DEATH] ═══ Processing death for: {sanitizedName} ═══");

                // STEP 1: Detect killer
                KillerDetectionService.KillerInfo killerInfo = null;
                if (character != null)
                {
                    killerInfo = _killerDetector.DetectKiller(character);
                    LoggerUtil.LogDebug(
                        $"[DEATH] Killer: {killerInfo.KillerName}, Weapon: {killerInfo.WeaponName}, Cause: {killerInfo.Cause}"
                    );
                }
                else
                {
                    LoggerUtil.LogWarning("[DEATH] Character is null - cannot detect killer");
                    killerInfo = new KillerDetectionService.KillerInfo();
                }

                // STEP 2: Detect location
                LocationZoneResult locationInfo = null;
                if (character != null && _config?.Death?.EnableLocationZones == true)
                {
                    locationInfo = _locationService.DetectDeathZone(character);
                    LoggerUtil.LogDebug(
                        $"[DEATH] Location: Zone={locationInfo.Zone}, Planet={locationInfo.PlanetName ?? "N/A"}"
                    );
                }
                else
                {
                    locationInfo = new LocationZoneResult();
                }

                // STEP 3: Generate message
                string deathMessage = GenerateDeathMessage(sanitizedName, killerInfo, locationInfo);
                LoggerUtil.LogInfo($"[DEATH] Generated: {deathMessage}");

                // STEP 4: Send to game
                SendToGameChat(deathMessage);

                // STEP 5: Send to Discord
                string discordMessage = AddEmotePrefix(deathMessage);
                await SendToDiscordAsync(discordMessage);

                LoggerUtil.LogSuccess($"[DEATH] ═══ Complete for {sanitizedName} ═══");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_HANDLER] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Generate death message with all context
        /// </summary>
        private string GenerateDeathMessage(
            string victimName,
            KillerDetectionService.KillerInfo killerInfo,
            LocationZoneResult locationInfo
        )
        {
            try
            {
                // Determine death type
                DeathTypeEnum deathType = DetermineDeathType(killerInfo);

                // Get template
                string template = _deathMessagesConfig.GetRandomMessage(deathType);

                // Build message
                string message = template;

                // Replace victim (sanitized name)
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

                // Add location
                string locationText = GenerateLocationText(locationInfo);
                if (!string.IsNullOrEmpty(locationText))
                {
                    message = $"{message} {locationText}";
                }

                return message;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_MSG] Error: {ex.Message}");
                return $"{victimName} died";
            }
        }

        /// <summary>
        /// ENHANCED: Determine death type with Turret distinction
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
                    return DeathTypeEnum.Turret; // Use TURRET templates!

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
        /// Get killer display name (sanitized)
        /// </summary>
        private string GetKillerDisplayName(KillerDetectionService.KillerInfo killerInfo)
        {
            if (killerInfo == null)
                return "Unknown";

            // NPC faction - use tag directly (no sanitization needed for faction tags)
            if (killerInfo.IsNpcFaction && !string.IsNullOrEmpty(killerInfo.NpcFactionTag))
            {
                return killerInfo.NpcFactionTag; // e.g. "SPID", "RUST"
            }

            // Player-owned turret or direct player kill - sanitize name
            if (!string.IsNullOrEmpty(killerInfo.KillerName))
            {
                return TextSanitizationUtil.SanitizePlayerName(killerInfo.KillerName);
            }

            return "Unknown";
        }

        /// <summary>
        /// Get weapon display name
        /// </summary>
        private string GetWeaponDisplayName(KillerDetectionService.KillerInfo killerInfo)
        {
            if (killerInfo == null || string.IsNullOrEmpty(killerInfo.WeaponName))
                return "Unknown";

            return killerInfo.WeaponName;
        }

        /// <summary>
        /// Generate location text with surface/orbit support
        /// </summary>
        private string GenerateLocationText(LocationZoneResult locationInfo)
        {
            if (locationInfo == null || !(_config?.Death?.EnableLocationZones ?? false))
                return "";

            try
            {
                bool showGridName = _config?.Death?.ShowGridName ?? true;
                return _locationService.GenerateLocationText(locationInfo, showGridName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_LOC] Error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Send to game chat
        /// </summary>
        private void SendToGameChat(string deathMessage)
        {
            try
            {
                MyVisualScriptLogicProvider.SendChatMessage(deathMessage, "Server", 0, "Red");
                LoggerUtil.LogDebug($"[DEATH_GAME] Sent: {deathMessage}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_GAME] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Add emoticon
        /// </summary>
        private string AddEmotePrefix(string message)
        {
            try
            {
                if (
                    _config?.Death == null
                    || string.IsNullOrEmpty(_config.Death.DeathMessageEmotes)
                )
                    return $"💀 {message}";

                var emotes = _config.Death.DeathMessageEmotes.Split(',');
                if (emotes.Length == 0)
                    return $"💀 {message}";

                string randomEmote = emotes[new Random().Next(emotes.Length)].Trim();
                return $"{randomEmote} {message}";
            }
            catch
            {
                return $"💀 {message}";
            }
        }

        /// <summary>
        /// Send to Discord
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

                await _eventLog.LogDeathAsync(discordMessage);
                LoggerUtil.LogDebug($"[DEATH_DISCORD] Sent: {discordMessage}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_DISCORD] Error: {ex.Message}");
            }
        }
    }
}
