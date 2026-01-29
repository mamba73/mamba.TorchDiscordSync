// Handlers\ChatModerator.cs
using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Handlers
{
    // Class for tracking user violations
    public class UserViolationRecord
    {
        public ulong UserId { get; set; }
        public int WarningCount { get; set; }
        public int MuteCount { get; set; }
        public DateTime LastMuteTime { get; set; }
        public bool IsMuted { get; set; }
        public DateTime MuteEndTime { get; set; }
    }

    public class ChatModerator
    {
        private readonly MainConfig _config;
        private readonly DiscordService _discordService;
        private readonly DatabaseService _db;
        
        // Dictionary for tracking user violations
        private Dictionary<ulong, UserViolationRecord> _userViolations = new Dictionary<ulong, UserViolationRecord>();
        private object _lockObject = new object();

        public ChatModerator(MainConfig config, DiscordService discordService, DatabaseService db)
        {
            _config = config;
            _discordService = discordService;
            _db = db;
        }

        public ChatModerator(MainConfig config, DiscordService discordService)
        {
            _config = config;
            _discordService = discordService;
        }

        // Process Discord message with moderation
        public void ProcessDiscordMessage(string message, string username, ulong channelId, ulong userId = 0)
        {
            try
            {
                // Check if moderation is enabled
                if (_config.Chat.EnableModeration && userId != 0)
                {
                    // Check if user is muted
                    if (IsUserMuted(userId))
                    {
                        LoggerUtil.LogInfo($"Blocked message from muted user {username}");
                        return;
                    }

                    string foundWord;
                    bool hasBlacklistedContent = ContainsBlacklistedWords(message, out foundWord) || 
                                               ContainsAttachmentsOrLinks(message);

                    if (hasBlacklistedContent)
                    {
                        // Add violation to user
                        var record = AddViolation(userId, username, message, "warning");
                        
                        // Check if user should be muted
                        if (record.WarningCount >= _config.Chat.MaxWarningsBeforeMute)
                        {
                            // Mute user
                            record.IsMuted = true;
                            record.MuteEndTime = DateTime.Now.AddMinutes(_config.Chat.MuteDurationMinutes);
                            record.MuteCount++;
                            record.WarningCount = 0;
                            
                            // Send mute message
                            string muteMsg = _config.Chat.MuteMessage.Replace("{minutes}", _config.Chat.MuteDurationMinutes.ToString());
                            // TODO: Send DM to user
                            
                            // Check if user should be kicked
                            if (record.MuteCount >= _config.Chat.MaxMutesBeforeKick)
                            {
                                record.MuteCount = 0; // Reset for kick
                                string kickMsg = _config.Chat.KickMessage;
                                // TODO: Kick user from channel
                            }
                        }
                        else
                        {
                            // Send warning message
                            // TODO: Send warning DM to user
                        }
                        
                        LoggerUtil.LogWarning($"Blocked message from {username}: contains '{foundWord}'");
                        return;
                    }
                }

                // Process normal message
                ProcessNormalDiscordMessage(message, username, channelId);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error processing Discord message: " + ex.Message);
            }
        }

        // Process normal Discord message (without moderation)
        private void ProcessNormalDiscordMessage(string message, string username, ulong channelId)
        {
            try
            {
                if (_db == null)
                    return;

                var factions = _db.GetAllFactions();
                if (factions == null || factions.Count == 0)
                    return;

                var faction = null as Models.FactionModel;
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].DiscordChannelID == channelId)
                    {
                        faction = factions[i];
                        break;
                    }
                }

                if (faction == null)
                    return;

                string sanitizedMsg = SecurityUtil.SanitizeMessage(message);
                string formattedMsg = "[" + faction.Tag + " - Discord] " + username + ": " + sanitizedMsg;

                Console.WriteLine("[CHAT_SYNC] " + formattedMsg);

                if (_config != null && _config.Debug)
                {
                    LoggerUtil.LogDebug("Discord -> Game (" + faction.Tag + "): " + formattedMsg);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("ChatSync error: " + ex.Message);
            }
        }

        // Process game message to Discord
        public void ProcessGameMessage(string message, string playerName, long steamId)
        {
            try
            {
                // Check moderation if enabled (for game messages too)
                if (_config.Chat.EnableModeration)
                {
                    string foundWord;
                    if (ContainsBlacklistedWords(message, out foundWord) || ContainsAttachmentsOrLinks(message))
                    {
                        LoggerUtil.LogWarning($"Blocked message from {playerName}: contains blacklisted content");
                        return; // Block message
                    }
                }

                if (_db == null)
                    return;

                var factions = _db.GetAllFactions();
                if (factions == null || factions.Count == 0)
                    return;

                var playerFaction = null as Models.FactionModel;
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].Players != null)
                    {
                        for (int j = 0; j < factions[i].Players.Count; j++)
                        {
                            if (factions[i].Players[j].SteamID == steamId)
                            {
                                playerFaction = factions[i];
                                break;
                            }
                        }
                    }
                    if (playerFaction != null) break;
                }

                if (playerFaction == null)
                    return;

                string sanitizedMsg = SecurityUtil.SanitizeMessage(message);
                string formattedMsg = playerName + ": " + sanitizedMsg;

                if (playerFaction.DiscordChannelID != 0 && _discordService != null)
                {
                    // LINIJA 198 - POPRAVLJENO: Dodan await
                    var _ = _discordService.SendLogAsync(playerFaction.DiscordChannelID, formattedMsg);
                }

                if (_config != null && _config.Debug)
                {
                    LoggerUtil.LogDebug("Game -> Discord (" + playerFaction.Tag + "): " + formattedMsg);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("ChatSync error: " + ex.Message);
            }
        }

        // Check if user is muted
        private bool IsUserMuted(ulong userId)
        {
            lock (_lockObject)
            {
                if (_userViolations.ContainsKey(userId))
                {
                    var record = _userViolations[userId];
                    if (record.IsMuted && DateTime.Now < record.MuteEndTime)
                    {
                        return true;
                    }
                    else if (record.IsMuted && DateTime.Now >= record.MuteEndTime)
                    {
                        // Mute expired
                        record.IsMuted = false;
                    }
                }
            }
            return false;
        }

        // Add violation to user
        private UserViolationRecord AddViolation(ulong userId, string username, string message, string violationType)
        {
            UserViolationRecord record;
            
            lock (_lockObject)
            {
                if (!_userViolations.ContainsKey(userId))
                {
                    _userViolations[userId] = new UserViolationRecord { UserId = userId };
                }
                
                record = _userViolations[userId];
                
                if (violationType == "warning")
                {
                    record.WarningCount++;
                }
                else if (violationType == "mute")
                {
                    record.MuteCount++;
                    record.WarningCount = 0; // Reset warnings
                }
            }

            // Log violation to admin channel
            if (_config.Chat.AdminLogChannelId != 0 && _discordService != null)
            {
                string logMessage = $"🛡️ **Chat Violation**\nUser: {username} ({userId})\nMessage: {message}\nType: {violationType}\nWarnings: {record.WarningCount}, Mutes: {record.MuteCount}";
                // LINIJA 263 - POPRAVLJENO: Dodan await
                var _ = _discordService.SendLogAsync(_config.Chat.AdminLogChannelId, logMessage);
            }

            return record;
        }

        // Check for blacklisted words
        private bool ContainsBlacklistedWords(string message, out string foundWord)
        {
            foundWord = null;
            if (_config.Chat.BlacklistedWords == null || _config.Chat.BlacklistedWords.Length == 0)
                return false;

            string lowerMessage = message.ToLower();
            for (int i = 0; i < _config.Chat.BlacklistedWords.Length; i++)
            {
                string word = _config.Chat.BlacklistedWords[i].ToLower();
                if (lowerMessage.Contains(word))
                {
                    foundWord = word;
                    return true;
                }
            }
            return false;
        }

        // Check for attachments or links
        private bool ContainsAttachmentsOrLinks(string message)
        {
            // Check for http/https links
            if (message.Contains("http://") || message.Contains("https://"))
                return true;
                
            // Check for Discord attachment formats
            if (message.Contains("```") && message.Contains("attachment"))
                return true;
                
            return false;
        }
    }
}
