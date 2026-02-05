// Utils/ChatUtils.cs - UPDATED WITH PRIVATE MESSAGE SUPPORT

using System;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game;

namespace mamba.TorchDiscordSync.Utils
{
    /// <summary>
    /// Chat utility methods for sending messages to players in-game
    /// Supports both broadcast and private messages
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
        /// Shows as: [TDS] ✅ Message
        /// </summary>
        public static void SendCommandResponse(string playerName, string result)
        {
            try
            {
                string message = $"✅ {result}";
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
        /// Shows as: [TDS] ⚠️ Message
        /// </summary>
        public static void SendWarning(string message)
        {
            try
            {
                string formattedMsg = $"⚠️ {message}";
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
        /// Shows as: [TDS] ❌ Message
        /// </summary>
        public static void SendError(string message)
        {
            try
            {
                string formattedMsg = $"❌ {message}";
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
        /// Shows as: [TDS] ✅ Message
        /// </summary>
        public static void SendSuccess(string message)
        {
            try
            {
                string formattedMsg = $"✅ {message}";
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
        /// Shows as: [TDS] ℹ️ Message
        /// </summary>
        public static void SendInfo(string message)
        {
            try
            {
                string formattedMsg = $"ℹ️ {message}";
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
    }
}
