// Services/ChatSyncService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    public class ChatSyncService
    {
        private readonly DiscordService _discord;
        private readonly MainConfig _config;
        private readonly DatabaseService _db;

        public ChatSyncService(DiscordService discord, MainConfig config, DatabaseService db)
        {
            _discord = discord;
            _config = config;
            _db = db;
        }

        public Task SyncChatAsync(string message, string playerName, long steamId)
        {
            if (_config != null && _config.Debug)
            {
                LoggerUtil.LogInfo("Chat sync: " + message);
            }
            return Task.FromResult(0);
        }

        public async Task SendGameChatToDiscordAsync(long playerSteamID, string playerName, string message)
        {
            try
            {
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
                            if (factions[i].Players[j].SteamID == playerSteamID)
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

                if (playerFaction.DiscordChannelID != 0 && _discord != null)
                {
                    await _discord.SendLogAsync(playerFaction.DiscordChannelID, formattedMsg);
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

        public async Task SendDiscordChatToGameAsync(string discordUsername, string message, ulong channelId)
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
                string formattedMsg = "[" + faction.Tag + " - Discord] " + discordUsername + ": " + sanitizedMsg;

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
    }
}