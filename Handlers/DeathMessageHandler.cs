// Handlers/DeathMessageHandler.cs
using System;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game;

namespace mamba.TorchDiscordSync.Handlers
{
    /// <summary>
    /// Handles death message generation, formatting, and delivery to both game and Discord.
    /// Generates a single unified message from death message templates and adds emoticons for Discord.
    /// </summary>
    public class DeathMessageHandler
    {
        private readonly EventLoggingService _eventLog;
        private readonly MainConfig _config;
        private readonly DeathMessagesConfig _deathMessagesConfig;

        public DeathMessageHandler(EventLoggingService eventLog, MainConfig config)
        {
            _eventLog = eventLog;
            _config = config;
            _deathMessagesConfig = DeathMessagesConfig.Load();
            LoggerUtil.LogDebug("[DEATH_HANDLER] Initialized");
        }

        /// <summary>
        /// Process a player death: generate message, send to game, and send to Discord.
        /// STEP A: Generate one message from template
        /// STEP B: Send to game (filter will block from Discord)
        /// STEP C: Add emoticon and send to Discord
        /// </summary>
        public async Task HandlePlayerDeathAsync(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo($"[DEATH] Processing death for player: {playerName}");

                // STEP A: Generate unified death message
                string deathMessage = GenerateUnifiedDeathMessage(playerName);
                LoggerUtil.LogDebug($"[DEATH_FLOW] Generated message: {deathMessage}");

                // STEP B: Send to game - filter will block death messages from Discord forwarding
                SendToGameChat(deathMessage);

                // STEP C: Add emoticon and send to Discord
                string discordMessage = AddEmotePrefix(deathMessage);
                LoggerUtil.LogDebug($"[DEATH_FLOW] Discord version: {discordMessage}");

                await SendToDiscordAsync(discordMessage);

                LoggerUtil.LogSuccess("[DEATH] Death message processing complete");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_HANDLER] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// STEP A: Generate death message from random template in DeathMessagesConfig
        /// </summary>
        private string GenerateUnifiedDeathMessage(string playerName)
        {
            try
            {
                if (_deathMessagesConfig == null)
                {
                    LoggerUtil.LogWarning("[DEATH] DeathMessagesConfig not loaded");
                    return $"{playerName} died";
                }

                // Use Accident type as it's neutral for all death scenarios
                string template = _deathMessagesConfig.GetRandomMessage(DeathTypeEnum.Accident);

                if (string.IsNullOrEmpty(template))
                {
                    return $"{playerName} died";
                }

                // Replace all placeholders
                string message = template
                    .Replace("{victim}", playerName)
                    .Replace("{killer}", "Unknown")
                    .Replace("{weapon}", "Unknown")
                    .Replace("{location}", "Unknown")
                    .Replace("{0}", "Unknown")
                    .Replace("{1}", playerName)
                    .Replace("{2}", "Unknown")
                    .Replace("{3}", "Unknown");

                LoggerUtil.LogDebug($"[DEATH] Generated from template: {message}");
                return message;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_GEN] Error generating message: {ex.Message}");
                return $"{playerName} died";
            }
        }

        /// <summary>
        /// STEP B: Send death message to in-game chat
        /// Format: [Server] message (filter will detect and block from Discord)
        /// </summary>
        private void SendToGameChat(string deathMessage)
        {
            try
            {
                LoggerUtil.LogDebug($"[DEATH_GAME] Sending to game: {deathMessage}");
                MyVisualScriptLogicProvider.SendChatMessage(deathMessage, "Server", 0, "Red");
                LoggerUtil.LogInfo($"[DEATH_GAME] Broadcasted to game: {deathMessage}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_GAME] Failed to broadcast: {ex.Message}");
            }
        }

        /// <summary>
        /// STEP C: Add random emoticon from configuration
        /// Emoticons are read from MainConfig.Death.DeathMessageEmotes
        /// </summary>
        private string AddEmotePrefix(string message)
        {
            try
            {
                // Default emoticon if no config available
                if (
                    _config?.Death == null
                    || string.IsNullOrEmpty(_config.Death.DeathMessageEmotes)
                )
                    return $"📢 {message}";

                var emotes = _config.Death.DeathMessageEmotes.Split(',');
                if (emotes.Length == 0)
                    return $"📢 {message}";

                string randomEmote = emotes[new Random().Next(emotes.Length)].Trim();
                LoggerUtil.LogDebug($"[DEATH_EMOTE] Selected emoticon: {randomEmote}");
                return $"{randomEmote} {message}";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_EMOTE] Error: {ex.Message}");
                return $"📢 {message}";
            }
        }

        /// <summary>
        /// Send death message directly to Discord (not through chat filter)
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

                LoggerUtil.LogDebug($"[DEATH_DISCORD] Sending to Discord: {discordMessage}");
                await _eventLog.LogDeathAsync(discordMessage);
                LoggerUtil.LogSuccess("[DEATH_DISCORD] Message sent to Discord");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DEATH_DISCORD] Failed: {ex.Message}");
            }
        }
    }
}
