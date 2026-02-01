// Services/FactionSyncService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Synchronizes Space Engineers factions with Discord roles and channels
    /// </summary>
    public class FactionSyncService
    {
        private readonly DatabaseService _db;
        private readonly DiscordService _discord;
        // NEW: Reference to config for checking if faction sync is enabled
        private readonly MainConfig _config;

        public FactionSyncService(DatabaseService db, DiscordService discord, MainConfig config)
        {
            _db = db;
            _discord = discord;
            _config = config;
        }

        /// <summary>
        /// Synchronize all player factions to Discord
        /// </summary>
        public async Task SyncFactionsAsync(List<FactionModel> factions)
        {
            // NEW: Skip entire sync if faction sync is disabled in config
            if (_config.Faction == null || !_config.Faction.Enabled)
            // if (_config?.Faction?.Enabled != true)
            {
                LoggerUtil.LogDebug("Faction sync is disabled in config - skipping synchronization");
                return;
            }
            try
            {
                LoggerUtil.LogInfo("[FACTION_SYNC] Starting faction synchronization");

                foreach (var faction in factions)
                {
                    if (faction.Tag.Length != 3)
                        continue;

                    var existing = _db.GetFaction(faction.FactionID);

                    if (existing == null)
                    {
                        faction.DiscordRoleID = await _discord.CreateRoleAsync(faction.Tag);
                        faction.DiscordChannelID = await _discord.CreateChannelAsync(faction.Name.ToLower());
                        _db.SaveFaction(faction);
                        LoggerUtil.LogInfo("[FACTION_SYNC] New faction created: " + faction.Tag + " - " + faction.Name);
                    }
                    else
                    {
                        faction.DiscordRoleID = existing.DiscordRoleID;
                        faction.DiscordChannelID = existing.DiscordChannelID;
                        _db.SaveFaction(faction);
                    }

                    if (faction.Players != null)
                    {
                        foreach (var player in faction.Players)
                        {
                            player.SyncedNick = "[" + faction.Tag + "] " + player.OriginalNick;
                            var playerModel = new PlayerModel
                            {
                                PlayerID = player.PlayerID,
                                SteamID = player.SteamID,
                                OriginalNick = player.OriginalNick,
                                SyncedNick = player.SyncedNick,
                                FactionID = faction.FactionID,
                                DiscordUserID = player.DiscordUserID
                            };
                            _db.SavePlayer(playerModel);
                        }
                    }
                }

                LoggerUtil.LogInfo("[FACTION_SYNC] Synchronization complete");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[FACTION_SYNC] Sync error: " + ex.Message);
            }
        }

        /// <summary>
        /// Reset Discord (delete all roles and channels created by plugin)
        /// WARNING: This is destructive!
        /// </summary>
        public async Task ResetDiscordAsync()
        {
            try
            {
                LoggerUtil.LogWarning("[FACTION_SYNC] Starting Discord reset (DESTRUCTIVE)");

                var factions = _db.GetAllFactions();
                if (factions != null)
                {
                    foreach (var faction in factions)
                    {
                        if (faction.DiscordRoleID != 0)
                        {
                            await _discord.DeleteRoleAsync(faction.DiscordRoleID);
                            LoggerUtil.LogInfo("[FACTION_SYNC] Deleted role: " + faction.Tag);
                        }

                        if (faction.DiscordChannelID != 0)
                        {
                            await _discord.DeleteChannelAsync(faction.DiscordChannelID);
                            LoggerUtil.LogInfo("[FACTION_SYNC] Deleted channel: " + faction.Name);
                        }
                    }
                }

                _db.ClearAllData();
                LoggerUtil.LogSuccess("[FACTION_SYNC] Discord reset complete");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[FACTION_SYNC] Reset error: " + ex.Message);
            }
        }
    }
}