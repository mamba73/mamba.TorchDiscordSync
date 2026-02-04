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
        private readonly MainConfig _config;
        private readonly FactionReaderService _factionReader;

        /// <summary>
        /// Initialize FactionSyncService with required dependencies
        /// </summary>
        public FactionSyncService(
            DatabaseService db,
            DiscordService discord,
            MainConfig config,
            FactionReaderService factionReader
        )
        {
            _db = db;
            _discord = discord;
            _config = config;
            _factionReader = factionReader;

            LoggerUtil.LogDebug("[FACTION_SYNC] FactionSyncService initialized");
        }

        /// <summary>
        /// Loads real factions from Space Engineers game session
        /// Replaces test data with actual faction data
        /// </summary>
        public List<FactionModel> LoadFactionsFromGame()
        {
            var factions = new List<FactionModel>();
            try
            {
                if (_factionReader == null)
                {
                    LoggerUtil.LogWarning("FactionReaderService is null - cannot load factions");
                    return factions;
                }

                // Load real faction data from game
                factions = _factionReader.LoadFactionsFromGame();
                LoggerUtil.LogInfo($"Loaded {factions.Count} factions from game session");

                return factions;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error loading factions from game: " + ex.Message);
                return factions;
            }
        }

        /// <summary>
        /// Synchronize all player factions to Discord
        /// Creates Discord roles and channels for each faction
        /// </summary>
        public async Task SyncFactionsAsync(List<FactionModel> factions = null)
        {
            // Check if faction sync is enabled in config
            if (_config?.Faction?.Enabled != true)
            {
                LoggerUtil.LogDebug("[FACTION_SYNC] Faction sync is disabled in config - skipping");
                return;
            }

            try
            {
                LoggerUtil.LogInfo("[FACTION_SYNC] Starting faction synchronization");

                // If no factions provided, load from game
                if (factions == null || factions.Count == 0)
                {
                    factions = LoadFactionsFromGame();
                }

                if (factions == null || factions.Count == 0)
                {
                    LoggerUtil.LogWarning("[FACTION_SYNC] No factions to synchronize");
                    return;
                }

                foreach (var faction in factions)
                {
                    // Skip factions with invalid tag length (should be 3 characters)
                    if (faction.Tag == null || faction.Tag.Length != 3)
                    {
                        LoggerUtil.LogDebug(
                            $"[FACTION_SYNC] Skipping faction with invalid tag: {faction.Tag}"
                        );
                        continue;
                    }

                    var existing = _db.GetFaction(faction.FactionID);

                    if (existing == null)
                    {
                        // New faction - create Discord role and channel
                        faction.DiscordRoleID = await _discord.CreateRoleAsync(faction.Tag);
                        faction.DiscordChannelID = await _discord.CreateChannelAsync(
                            faction.Name.ToLower()
                        );
                        _db.SaveFaction(faction);
                        LoggerUtil.LogInfo(
                            $"[FACTION_SYNC] Created new faction: {faction.Tag} - {faction.Name}"
                        );
                    }
                    else
                    {
                        // Existing faction - update with stored Discord IDs
                        faction.DiscordRoleID = existing.DiscordRoleID;
                        faction.DiscordChannelID = existing.DiscordChannelID;
                        _db.SaveFaction(faction);
                        LoggerUtil.LogDebug(
                            $"[FACTION_SYNC] Updated existing faction: {faction.Tag}"
                        );
                    }

                    // Sync faction members
                    if (faction.Players != null && faction.Players.Count > 0)
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
                                DiscordUserID = player.DiscordUserID,
                            };
                            _db.SavePlayer(playerModel);
                        }
                        LoggerUtil.LogDebug(
                            $"[FACTION_SYNC] Synced {faction.Players.Count} players for {faction.Tag}"
                        );
                    }
                }

                LoggerUtil.LogSuccess("[FACTION_SYNC] Synchronization complete");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[FACTION_SYNC] Sync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset Discord - delete all roles and channels created by plugin
        /// WARNING: This is destructive!
        /// </summary>
        public async Task ResetDiscordAsync()
        {
            try
            {
                LoggerUtil.LogWarning(
                    "[FACTION_SYNC] Starting Discord reset (DESTRUCTIVE OPERATION)"
                );

                var factions = _db.GetAllFactions();
                if (factions != null && factions.Count > 0)
                {
                    foreach (var faction in factions)
                    {
                        // Delete Discord role
                        if (faction.DiscordRoleID != 0)
                        {
                            await _discord.DeleteRoleAsync(faction.DiscordRoleID);
                            LoggerUtil.LogInfo(
                                $"[FACTION_SYNC] Deleted Discord role for: {faction.Tag}"
                            );
                        }

                        // Delete Discord channel
                        if (faction.DiscordChannelID != 0)
                        {
                            await _discord.DeleteChannelAsync(faction.DiscordChannelID);
                            LoggerUtil.LogInfo(
                                $"[FACTION_SYNC] Deleted Discord channel for: {faction.Name}"
                            );
                        }
                    }
                }

                // Clear all local database
                _db.ClearAllData();
                LoggerUtil.LogSuccess("[FACTION_SYNC] Discord reset complete - all data cleared");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[FACTION_SYNC] Reset error: {ex.Message}");
            }
        }
    }
}
