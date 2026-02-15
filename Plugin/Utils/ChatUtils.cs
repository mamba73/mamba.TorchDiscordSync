// Plugin/Utils/ChatUtils.cs
using System;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;
using Sandbox.Game;

namespace mamba.TorchDiscordSync.Plugin.Utils
{
    /// <summary>
    /// Chat utility methods for sending messages to players in-game
    /// Supports both broadcast and private messages
    /// Uses ASCII characters instead of emoji for Space Engineers game chat compatibility
    /// </summary>
    public static class ChatUtils
    {
        // Prefix for private messages - used to filter out from Discord forwarding
        private const string PRIVATE_PREFIX = "[PRIVATE_CMD]";

        /// <summary>
        /// Send message to server console only
        /// </summary>
        public static void SendServerMessage(string message)
        {
            try
            {
                Console.WriteLine($"[SERVER] {message}");
                LoggerUtil.LogDebug($"[CONSOLE] {message}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Chat message send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send broadcast message to ALL players in-game
        /// </summary>
        public static void BroadcastToServer(string message)
        {
            try
            {
                LoggerUtil.LogDebug($"[BROADCAST] {message}");
                MyVisualScriptLogicProvider.SendChatMessage(message, "Server", 0, "White");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Broadcast error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send command response to player (PRIVATE - not forwarded to Discord)
        /// Shows as: [TDS] [OK] Message
        /// </summary>
        public static void SendCommandResponse(string playerName, string result)
        {
            try
            {
                string message = $"[OK] {result}";
                LoggerUtil.LogDebug($"[CMD_RESPONSE] {playerName}: {message}");
                SendPrivateToPlayer(message, "Green");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Command response error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send warning message to player (PRIVATE)
        /// Shows as: [TDS] [!] Message
        /// </summary>
        public static void SendWarning(string message)
        {
            try
            {
                string formattedMsg = $"[!] {message}";
                LoggerUtil.LogDebug($"[WARNING] {message}");
                SendPrivateToPlayer(formattedMsg, "Yellow");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Warning error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send error message to player (PRIVATE)
        /// Shows as: [TDS] [FAIL] Message
        /// </summary>
        public static void SendError(string message)
        {
            try
            {
                string formattedMsg = $"[FAIL] {message}";
                LoggerUtil.LogDebug($"[ERROR] {message}");
                SendPrivateToPlayer(formattedMsg, "Red");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Send success message to player (PRIVATE)
        /// Shows as: [TDS] [OK] Message
        /// </summary>
        public static void SendSuccess(string message)
        {
            try
            {
                string formattedMsg = $"[OK] {message}";
                LoggerUtil.LogDebug($"[SUCCESS] {message}");
                SendPrivateToPlayer(formattedMsg, "Green");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Success send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Send info message to player (PRIVATE)
        /// Shows as: [TDS] [I] Message
        /// </summary>
        public static void SendInfo(string message)
        {
            try
            {
                string formattedMsg = $"[I] {message}";
                LoggerUtil.LogDebug($"[INFO] {message}");
                SendPrivateToPlayer(formattedMsg, "Blue");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Info send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal method - sends PRIVATE message to player in-game chat
        /// Messages are prefixed with PRIVATE_PREFIX so OnChatMessageProcessing can filter them
        /// This prevents command responses from being forwarded to Discord
        /// </summary>
        private static void SendPrivateToPlayer(string message, string color)
        {
            try
            {
                // Prefix with PRIVATE_PREFIX so it can be filtered out
                string prefixedMessage = $"{PRIVATE_PREFIX} {message}";
                MyVisualScriptLogicProvider.SendChatMessage(prefixedMessage, "TDS", 0, color);
                LoggerUtil.LogDebug($"[CHAT_SENT] [{color}] {message}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Failed to send private chat message: {ex.Message}");
                // Fallback: at least log it
                SendServerMessage(message);
            }
        }

        /// <summary>
        /// Send multi-line help text to player (PRIVATE - not forwarded to Discord)
        /// </summary>
        public static void SendHelpText(string helpText)
        {
            try
            {
                LoggerUtil.LogDebug($"[HELP] Sending help text to player");

                // Prefix with PRIVATE_PREFIX so OnChatMessageProcessing filters it out
                string prefixedText = $"{PRIVATE_PREFIX}\n{helpText}";
                MyVisualScriptLogicProvider.SendChatMessage(prefixedText, "TDS", 0, "Green");
                LoggerUtil.LogDebug($"[HELP_SENT] Help text marked as private");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Help text send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a message is marked as private (command response)
        /// Used in OnChatMessageProcessing to filter out private command responses
        /// </summary>
        public static bool IsPrivateMessage(string message)
        {
            return !string.IsNullOrEmpty(message) && message.Contains(PRIVATE_PREFIX);
        }

        /// <summary>
        /// Get the private message prefix constant
        /// Used by OnChatMessageProcessing filter
        /// </summary>
        public static string GetPrivateMessagePrefix()
        {
            return PRIVATE_PREFIX;
        }

        // ============================================================
        // PROCESS CHAT MESSAGE - MOVED FROM Plugin/index.cs
        // ============================================================

        /// <summary>
        /// Process incoming chat message and route it appropriately
        /// - System messages to player tracking
        /// - Regular chat to Discord sync
        /// - Commands are filtered out (handled separately)
        ///
        /// EXTRACTED from Plugin.ProcessChatMessage() - moved to utility for reusability
        /// </summary>
        public static void ProcessChatMessage(
            string message,
            string author,
            string channel,
            ChatSyncService chatSync,
            PlayerTrackingService playerTracking,
            MainConfig config
        )
        {
            LoggerUtil.LogDebug(
                $@"[CHAT PROCESS] Channel: {channel} | Author: {author} | Message: {message}"
            );

            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(author))
            {
                LoggerUtil.LogDebug($"[CHAT PROCESS] - returned due to null/empty");
                return;
            }

            // Prevent duplication: skip Server death messages that were already sent from death event
            if (author == "Server" && (message.Contains("died") || message.Contains("killed")))
            {
                LoggerUtil.LogDebug(
                    "[CHAT PROCESS] Skipped Server death message to prevent duplication on Discord"
                );
                return;
            }

            // System messages
            if (channel == "System" && playerTracking != null)
            {
                LoggerUtil.LogDebug("[CHAT PROCESS] Forwarding system message to tracking");
                playerTracking.ProcessSystemChatMessage(message);
                return;
            }

            // Normal chat → Discord
            if (chatSync != null && config?.Chat != null)
            {
                bool enabled = config.Chat.ServerToDiscord;
                LoggerUtil.LogDebug($"[CHAT PROCESS] ServerToDiscord enabled: {enabled}");

                if (enabled)
                {
                    if (message.StartsWith("/"))
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Skipped command");
                        return;
                    }

                    if (channel == "Global")
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Global chat - sending to Discord");
                        _ = chatSync.SendGameMessageToDiscordAsync(author, message);
                    }
                    else if (channel.StartsWith("Faction:"))
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Faction chat - skipped for now");
                    }
                    else if (channel == "Private")
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Private chat - skipped for security");
                    }
                    else
                    {
                        LoggerUtil.LogDebug("[CHAT PROCESS] Unknown channel - fallback to global");
                        _ = chatSync.SendGameMessageToDiscordAsync(author, message);
                    }
                }
                else
                {
                    LoggerUtil.LogDebug("[CHAT PROCESS] ServerToDiscord disabled in config");
                }
            }
            else
            {
                LoggerUtil.LogWarning("[CHAT PROCESS] ChatSyncService or config null");
            }
        }

    }
}
