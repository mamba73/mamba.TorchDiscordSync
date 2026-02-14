// Plugin/Utils/TextSanitizationUtil.cs
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace mamba.TorchDiscordSync.Plugin.Utils
{
    /// <summary>
    /// Utility for sanitizing text (player names, messages, etc.)
    /// Removes non-ASCII characters, control characters, and unwanted symbols
    /// </summary>
    public static class TextSanitizationUtil
    {
        /// <summary>
        /// Sanitize player name by removing non-ASCII and control characters
        /// Keeps only: A-Z, a-z, 0-9, space, underscore, hyphen
        /// </summary>
        public static string SanitizePlayerName(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return "Unknown";

            try
            {
                // Remove all non-ASCII characters (including monitor icons, emojis, etc.)
                string sanitized = Regex.Replace(playerName, @"[^\x20-\x7E]", "");

                // Additionally remove any remaining control characters
                sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

                // Trim whitespace
                sanitized = sanitized.Trim();

                // If nothing left after sanitization, return placeholder
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    LoggerUtil.LogWarning(
                        $"[SANITIZE] Player name completely removed: '{playerName}' -> using 'Player'"
                    );
                    return "Player";
                }

                // Log if name was changed
                if (sanitized != playerName)
                {
                    LoggerUtil.LogDebug(
                        $"[SANITIZE] Name cleaned: '{playerName}' -> '{sanitized}'"
                    );
                }

                return sanitized;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"[SANITIZE] Error sanitizing name '{playerName}': {ex.Message}"
                );
                return "Player";
            }
        }

        /// <summary>
        /// Sanitize chat message (more permissive than player names)
        /// Removes control characters but allows more unicode
        /// </summary>
        public static string SanitizeChatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";

            try
            {
                // Remove only control characters, keep unicode for multilingual support
                string sanitized = Regex.Replace(message, @"[\x00-\x1F\x7F]", "");

                return sanitized.Trim();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[SANITIZE] Error sanitizing message: {ex.Message}");
                return message;
            }
        }

        /// <summary>
        /// Remove Discord/Minecraft formatting characters that might cause issues
        /// </summary>
        public static string RemoveFormattingCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                // Remove common problematic characters
                text = text.Replace("", ""); // Zero-width space
                text = text.Replace("", ""); // Zero-width non-joiner
                text = text.Replace("​", ""); // Zero-width joiner

                return text;
            }
            catch
            {
                return text;
            }
        }
    }
}
