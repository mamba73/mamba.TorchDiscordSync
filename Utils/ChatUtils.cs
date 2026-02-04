// Utils/ChatUtils.cs
using System;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game;

namespace mamba.TorchDiscordSync.Utils
{
    /// <summary>
    /// Chat utility methods for sending messages to players in-game
    /// Sends responses from commands directly to players
    /// </summary>
    public static class ChatUtils
    {
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
        /// Send command response to player
        /// Shows as: [TDS] ✅ Message
        /// </summary>
        public static void SendCommandResponse(string playerName, string result)
        {
            try
            {
                string message = $"✅ {result}";
                LoggerUtil.LogDebug($"[CMD_RESPONSE] {playerName}: {message}");
                SendToPlayer(message, "Green");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Command response error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send warning message to player
        /// Shows as: [TDS] ⚠️ Message
        /// </summary>
        public static void SendWarning(string message)
        {
            try
            {
                string formattedMsg = $"⚠️ {message}";
                LoggerUtil.LogDebug($"[WARNING] {message}");
                SendToPlayer(formattedMsg, "Yellow");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Warning error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send error message to player
        /// Shows as: [TDS] ❌ Message
        /// </summary>
        public static void SendError(string message)
        {
            try
            {
                string formattedMsg = $"❌ {message}";
                LoggerUtil.LogDebug($"[ERROR] {message}");
                SendToPlayer(formattedMsg, "Red");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Send success message to player
        /// Shows as: [TDS] ✅ Message
        /// </summary>
        public static void SendSuccess(string message)
        {
            try
            {
                string formattedMsg = $"✅ {message}";
                LoggerUtil.LogDebug($"[SUCCESS] {message}");
                SendToPlayer(formattedMsg, "Green");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Success send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Send info message to player
        /// Shows as: [TDS] ℹ️ Message
        /// </summary>
        public static void SendInfo(string message)
        {
            try
            {
                string formattedMsg = $"ℹ️ {message}";
                LoggerUtil.LogDebug($"[INFO] {message}");
                SendToPlayer(formattedMsg, "Blue");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Info send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal method - sends message to player in-game chat
        /// </summary>
        private static void SendToPlayer(string message, string color)
        {
            try
            {
                MyVisualScriptLogicProvider.SendChatMessage(message, "TDS", 0, color);
                LoggerUtil.LogDebug($"[CHAT_SENT] [{color}] {message}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Failed to send chat message: {ex.Message}");
                // Fallback: at least log it
                SendServerMessage(message);
            }
        }

        /// <summary>
        /// Send multi-line help text to player
        /// </summary>
        public static void SendHelpText(string helpText)
        {
            try
            {
                LoggerUtil.LogDebug($"[HELP] Sending help text to player");
                MyVisualScriptLogicProvider.SendChatMessage(helpText, "TDS", 0, "Green");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Help text send failed: {ex.Message}");
            }
        }
    }
}
