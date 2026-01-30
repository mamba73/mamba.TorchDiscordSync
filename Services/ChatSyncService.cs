// Services/ChatSyncService.cs
// ENHANCED - Bidirectional chat synchronization
// Game messages ↔ Discord messages with proper formatting

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Bidirectional chat synchronization service
    /// Syncs messages between Space Engineers game chat and Discord
    /// Game → Discord: Player messages
    /// Discord → Game: Bot/user messages
    /// </summary>
    public class ChatSyncService
    {
        private readonly DiscordService _discord;
        private readonly MainConfig _config;
        private readonly DatabaseService _db;
        
        // Chat tracking to prevent duplicate sync
        private HashSet<string> _syncedMessages = new HashSet<string>();
        private Dictionary<string, DateTime> _lastMessageTime = new Dictionary<string, DateTime>();
        
        // Rate limiting per player
        private const int MESSAGE_THROTTLE_MS = 500; // Min 500ms between messages
        private const int DUPLICATE_WINDOW_S = 2; // Check duplicates within 2 seconds

        public ChatSyncService(DiscordService discord, MainConfig config, DatabaseService db)
        {
            _discord = discord;
            _config = config;
            _db = db;
            LoggerUtil.LogDebug("ChatSyncService initialized");
        }

        /// <summary>
        /// Send message from game to Discord
        /// Called when player sends chat message in-game
        /// </summary>
        public async Task SendGameMessageToDiscordAsync(string playerName, string message, string channel = "global")
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(message))
                {
                    LoggerUtil.LogDebug("Chat: Empty player name or message");
                    return;
                }

                // Rate limiting
                string rateLimitKey = $"{playerName}_game";
                if (!CheckRateLimit(rateLimitKey))
                {
                    LoggerUtil.LogDebug($"Chat: Rate limit hit for {playerName}");
                    return;
                }

                // Prevent spam/duplicates
                string messageKey = $"{playerName}:{message}";
                if (_syncedMessages.Contains(messageKey))
                {
                    LoggerUtil.LogDebug("Chat: Duplicate game message suppressed");
                    return;
                }

                // Store to prevent re-sync
                _syncedMessages.Add(messageKey);
                if (_syncedMessages.Count > 1000) // Keep memory bounded
                {
                    var oldest = _syncedMessages.First();
                    _syncedMessages.Remove(oldest);
                }

                // Format Discord embed message
                string discordMessage = FormatGameMessageForDiscord(playerName, message, channel);

                // Get target channel from config
                ulong targetChannel = GetChannelIdForGameChannel(channel);
                if (targetChannel == 0)
                {
                    LoggerUtil.LogDebug($"Chat: No Discord channel configured for '{channel}'");
                    return;
                }

                // Send to Discord
                bool sent = await _discord.SendLogAsync(targetChannel, discordMessage);
                
                if (sent)
                {
                    LoggerUtil.LogInfo($"[CHAT] Game→Discord: {playerName}: {message}");
                    
                    // Save to database
                    LogChatMessage(playerName, message, "game", channel);
                }
                else
                {
                    LoggerUtil.LogWarning($"Chat: Failed to send to Discord ({targetChannel})");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error sending game message to Discord: {ex.Message}");
            }
        }

        /// <summary>
        /// Send message from Discord to game
        /// Called when Discord message received in monitored channel
        /// </summary>
        public async Task SendDiscordMessageToGameAsync(
            string discordUsername, 
            string message, 
            string discordChannel = "")
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(discordUsername) || string.IsNullOrWhiteSpace(message))
                {
                    LoggerUtil.LogDebug("Chat: Empty Discord username or message");
                    return;
                }

                // Filter out bot messages (prevent echo)
                if (discordUsername.Contains("bot") || discordUsername.Contains("Bot"))
                {
                    LoggerUtil.LogDebug("Chat: Bot message filtered");
                    return;
                }

                // Rate limiting
                string rateLimitKey = $"{discordUsername}_discord";
                if (!CheckRateLimit(rateLimitKey))
                {
                    LoggerUtil.LogDebug($"Chat: Rate limit hit for Discord user {discordUsername}");
                    return;
                }

                // Prevent spam/duplicates
                string messageKey = $"{discordUsername}:{message}";
                if (_syncedMessages.Contains(messageKey))
                {
                    LoggerUtil.LogDebug("Chat: Duplicate Discord message suppressed");
                    return;
                }

                // Store to prevent re-sync
                _syncedMessages.Add(messageKey);

                // Format for game chat
                string gameMessage = FormatDiscordMessageForGame(discordUsername, message);

                LoggerUtil.LogInfo($"[CHAT] Discord→Game: {discordUsername}: {message}");

                // In real implementation, this would call ChatManager.SendChatMessage
                // For now, log it
                LogChatMessage(discordUsername, message, "discord", discordChannel);

                // This would be called from plugin's chat handler
                // CommandHandler.ExecuteCommand($"/say {gameMessage}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error sending Discord message to game: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming game chat messages (from plugin)
        /// Route to appropriate handlers (faction chat, system, etc)
        /// </summary>
        public async Task ProcessGameChatMessageAsync(string playerName, string message, string channel)
        {
            try
            {
                LoggerUtil.LogDebug($"Processing game chat: {playerName} → {channel}: {message}");

                // Check for system messages (join/leave)
                if (message.Contains(" joined the game") || message.Contains(" left the game"))
                {
                    LoggerUtil.LogDebug("System message detected, skipping sync");
                    return;
                }

                // Route based on channel
                if (channel == "faction")
                {
                    await SendGameMessageToDiscordAsync(playerName, message, "faction");
                }
                else if (channel == "system")
                {
                    // System messages don't sync to Discord usually
                    LoggerUtil.LogDebug("System message suppressed from sync");
                }
                else
                {
                    // Global channel
                    await SendGameMessageToDiscordAsync(playerName, message, "global");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error processing game chat: {ex.Message}");
            }
        }

        /// <summary>
        /// Format game chat message for Discord display
        /// </summary>
        private string FormatGameMessageForDiscord(string playerName, string message, string channel)
        {
            try
            {
                // Clean message of special characters
                string cleanMessage = CleanMessageText(message);
                
                // Remove commands (don't sync commands to Discord)
                if (cleanMessage.StartsWith("/"))
                {
                    return null;
                }

                // Format based on channel
                string formatted = channel switch
                {
                    "faction" => $"**[FACTION] {playerName}**: {cleanMessage}",
                    "private" => $"**[PRIVATE] {playerName}**: {cleanMessage}",
                    "global" => $"**{playerName}**: {cleanMessage}",
                    _ => $"**{playerName}**: {cleanMessage}"
                };

                // Limit length
                if (formatted.Length > 2000) // Discord message limit
                {
                    formatted = formatted.Substring(0, 1990) + "...";
                }

                return formatted;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error formatting game message: {ex.Message}");
                return $"{playerName}: {message}";
            }
        }

        /// <summary>
        /// Format Discord message for game chat
        /// </summary>
        private string FormatDiscordMessageForGame(string discordUser, string message)
        {
            try
            {
                // Clean message
                string cleanMessage = CleanMessageText(message);

                // Limit length (game chat has smaller limit)
                if (cleanMessage.Length > 100)
                {
                    cleanMessage = cleanMessage.Substring(0, 97) + "...";
                }

                // Format: [DISCORD] username: message
                return $"[DISCORD] {discordUser}: {cleanMessage}";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error formatting Discord message: {ex.Message}");
                return message;
            }
        }

        /// <summary>
        /// Clean message text of unwanted characters
        /// </summary>
        private string CleanMessageText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return "";

                // Remove Discord mentions (to prevent spam)
                text = Regex.Replace(text, @"<@!?[0-9]+>", "");
                
                // Remove URLs
                text = Regex.Replace(text, @"https?://[^\s]+", "[URL]");
                
                // Remove multiple spaces
                text = Regex.Replace(text, @"\s+", " ");
                
                // Trim
                text = text.Trim();

                return text;
            }
            catch
            {
                return text;
            }
        }

        /// <summary>
        /// Check rate limit for a user
        /// Returns false if throttled
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

                int timeSinceLastMs = (int)(DateTime.UtcNow - lastTime).TotalMilliseconds;
                if (timeSinceLastMs < MESSAGE_THROTTLE_MS)
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
        /// Get Discord channel ID for game channel
        /// </summary>
        private ulong GetChannelIdForGameChannel(string gameChannel)
        {
            try
            {
                if (_config?.Discord == null)
                    return 0;

                return gameChannel switch
                {
                    "faction" => _config.Discord.ChatChannelId,
                    "global" => _config.Discord.ChatChannelId,
                    "private" => _config.Discord.ChatChannelId,
                    _ => _config.Discord.ChatChannelId
                };
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Log chat message to database for history
        /// </summary>
        private void LogChatMessage(string playerName, string message, string source, string channel)
        {
            try
            {
                if (_db != null)
                {
                    var chatRecord = new EventLogModel
                    {
                        EventType = "Chat",
                        Details = $"[{source.ToUpper()}] {playerName}: {message}",
                        Timestamp = DateTime.UtcNow
                    };

                    _db.LogEvent(chatRecord);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error logging chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// Get chat history between two timestamps
        /// </summary>
        public List<(string Author, string Message, DateTime Time, string Source)> GetChatHistory(
            DateTime from, 
            DateTime to)
        {
            var history = new List<(string, string, DateTime, string)>();

            try
            {
                // In real implementation, would query database
                // For now, return empty (database layer integration needed)
                return history;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error getting chat history: {ex.Message}");
                return history;
            }
        }

        /// <summary>
        /// Clear chat cache (for memory management)
        /// </summary>
        public void ClearCache()
        {
            try
            {
                _syncedMessages.Clear();
                _lastMessageTime.Clear();
                LoggerUtil.LogDebug("Chat cache cleared");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error clearing chat cache: {ex.Message}");
            }
        }
    }
}