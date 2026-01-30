// Services/ChatSyncService.cs
// Bidirectional chat synchronization service
// Game messages → Discord global channel
// Discord messages → Game global chat (server-side broadcast)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.ModAPI;               // Za MyAPIGateway (ako bude trebalo)
using Sandbox.Game;                  // Za MyVisualScriptLogicProvider
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Bidirectional chat synchronization service
    /// Syncs messages between Space Engineers global chat and Discord channel
    /// </summary>
    public class ChatSyncService
    {
        private readonly DiscordService _discord;
        private readonly MainConfig _config;
        private readonly DatabaseService _db;

        // Tracking to prevent duplicate sync and spam
        private HashSet<string> _syncedMessages = new HashSet<string>();
        private Dictionary<string, DateTime> _lastMessageTime = new Dictionary<string, DateTime>();

        // Rate limiting constants
        private const int MESSAGE_THROTTLE_MS = 500;     // Minimum time between messages from same source
        private const int DUPLICATE_WINDOW_S = 2;       // Duplicate check window in seconds

        public ChatSyncService(DiscordService discord, MainConfig config, DatabaseService db)
        {
            _discord = discord;
            _config = config;
            _db = db;
            LoggerUtil.LogDebug("ChatSyncService initialized");
        }

        /// <summary>
        /// Send message from game to Discord global channel
        /// Called when player sends message in global chat
        /// </summary>
        public async Task SendGameMessageToDiscordAsync(string playerName, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(message))
                {
                    LoggerUtil.LogDebug("Chat: Empty player name or message - skipping");
                    return;
                }

                // Rate limiting per player
                string rateLimitKey = $"{playerName}_game";
                if (!CheckRateLimit(rateLimitKey))
                {
                    LoggerUtil.LogDebug($"Chat: Rate limit hit for player {playerName}");
                    return;
                }

                // Prevent duplicates
                string messageKey = $"{playerName}:{message}";
                if (_syncedMessages.Contains(messageKey))
                {
                    LoggerUtil.LogDebug("Chat: Duplicate game message suppressed");
                    return;
                }
                _syncedMessages.Add(messageKey);

                // Clean up old entries to prevent memory growth
                if (_syncedMessages.Count > 1000)
                {
                    var oldest = _syncedMessages.First();
                    _syncedMessages.Remove(oldest);
                }

                // Format message for Discord
                string discordMessage = FormatGameMessageForDiscord(playerName, message);

                // Skip if it's a command or empty after formatting
                if (string.IsNullOrWhiteSpace(discordMessage) || discordMessage.StartsWith("/"))
                {
                    return;
                }

                // Get target Discord channel from config
                ulong targetChannel = _config?.Discord?.ChatChannelId ?? 0;
                if (targetChannel == 0)
                {
                    LoggerUtil.LogWarning("Chat: No Discord ChatChannelId configured in config");
                    return;
                }

                // Send to Discord
                bool sent = await _discord.SendLogAsync(targetChannel, discordMessage);

                if (sent)
                {
                    LoggerUtil.LogInfo($"[CHAT] Game → Discord: {playerName}: {message}");
                    LogChatMessage(playerName, message, "game", "global");
                }
                else
                {
                    LoggerUtil.LogWarning($"Chat: Failed to send to Discord channel {targetChannel}");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error sending game message to Discord: {ex.Message}");
            }
        }

        /// <summary>
        /// Send message from Discord to game global chat
        /// Called when message received in monitored Discord channel
        /// </summary>
        public async Task SendDiscordMessageToGameAsync(string discordUsername, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(discordUsername) || string.IsNullOrWhiteSpace(message))
                {
                    LoggerUtil.LogDebug("Chat: Empty Discord username or message - skipping");
                    return;
                }

                // Filter bot messages to prevent echo loops
                if (discordUsername.Contains("bot", StringComparison.OrdinalIgnoreCase))
                {
                    LoggerUtil.LogDebug("Chat: Bot message filtered - skipping");
                    return;
                }

                // Rate limiting per Discord user
                string rateLimitKey = $"{discordUsername}_discord";
                if (!CheckRateLimit(rateLimitKey))
                {
                    LoggerUtil.LogDebug($"Chat: Rate limit hit for Discord user {discordUsername}");
                    return;
                }

                // Prevent duplicates
                string messageKey = $"{discordUsername}:{message}";
                if (_syncedMessages.Contains(messageKey))
                {
                    LoggerUtil.LogDebug("Chat: Duplicate Discord message suppressed");
                    return;
                }
                _syncedMessages.Add(messageKey);

                // Format message for game chat
                string gameMessage = FormatDiscordMessageForGame(discordUsername, message);

                LoggerUtil.LogInfo($"[CHAT] Discord → Game: {discordUsername}: {message}");

                // Send to global chat for ALL players (server-side broadcast)
                try
                {
                    // Use MyVisualScriptLogicProvider for server-wide chat broadcast
                    MyVisualScriptLogicProvider.SendChatMessage(
                        gameMessage,           // Message text
                        "Discord",             // Sender name shown in chat
                        0,                     // 0 = server-sent
                        "Blue"                 // Color: Blue, Green, Red, White, etc.
                    );

                    LoggerUtil.LogSuccess($"[CHAT] Broadcasted to game: {gameMessage}");
                    LogChatMessage(discordUsername, message, "discord", "global");
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Failed to broadcast chat message to game: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error sending Discord message to game: {ex.Message}");
            }
        }

        /// <summary>
        /// Format game chat message for Discord display
        /// </summary>
        private string FormatGameMessageForDiscord(string playerName, string message)
        {
            try
            {
                string cleanMessage = CleanMessageText(message);

                // Skip commands
                if (cleanMessage.StartsWith("/"))
                {
                    return null;
                }

                string formatted = $"**{playerName}**: {cleanMessage}";

                // Respect Discord message length limit
                if (formatted.Length > 2000)
                {
                    formatted = formatted.Substring(0, 1990) + "...";
                }

                return formatted;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error formatting game message for Discord: {ex.Message}");
                return $"{playerName}: {message}";
            }
        }

        /// <summary>
        /// Format Discord message for game chat display
        /// </summary>
        private string FormatDiscordMessageForGame(string discordUser, string message)
        {
            try
            {
                string cleanMessage = CleanMessageText(message);

                // Limit length for game chat readability
                if (cleanMessage.Length > 100)
                {
                    cleanMessage = cleanMessage.Substring(0, 97) + "...";
                }

                return $"[Discord] {discordUser}: {cleanMessage}";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error formatting Discord message for game: {ex.Message}");
                return message;
            }
        }

        /// <summary>
        /// Clean message text - remove mentions, URLs, extra spaces
        /// </summary>
        private string CleanMessageText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return "";

                // Remove Discord mentions to prevent spam
                text = Regex.Replace(text, @"<@!?[0-9]+>", "");

                // Replace URLs with placeholder
                text = Regex.Replace(text, @"https?://[^\s]+", "[URL]");

                // Normalize spaces
                text = Regex.Replace(text, @"\s+", " ");

                return text.Trim();
            }
            catch
            {
                return text;
            }
        }

        /// <summary>
        /// Check rate limit for a given key (player or user)
        /// </summary>
        private bool CheckRateLimit(string key)
        {
            try
            {
                if (!_lastMessageTime.TryGetValue(key, out var lastTime))
                {
                    _lastMessageTime[key] = DateTime.UtcNow;
                    return true;
                }

                int msSinceLast = (int)(DateTime.UtcNow - lastTime).TotalMilliseconds;
                if (msSinceLast < MESSAGE_THROTTLE_MS)
                {
                    return false; // Throttled
                }

                _lastMessageTime[key] = DateTime.UtcNow;
                return true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Log chat message to database for history/auditing
        /// </summary>
        private void LogChatMessage(string author, string message, string source, string channel)
        {
            try
            {
                if (_db != null)
                {
                    var evt = new EventLogModel
                    {
                        EventType = "Chat",
                        Details = $"[{source.ToUpper()}] {author}: {message} ({channel})",
                        Timestamp = DateTime.UtcNow
                    };
                    _db.LogEvent(evt);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error logging chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear internal caches (called on unload or periodically)
        /// </summary>
        public void ClearCache()
        {
            try
            {
                _syncedMessages.Clear();
                _lastMessageTime.Clear();
                LoggerUtil.LogDebug("Chat sync cache cleared");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error clearing chat cache: {ex.Message}");
            }
        }
    }
}
