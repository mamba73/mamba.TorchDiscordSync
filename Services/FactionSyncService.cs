// Services/FactionSyncService.cs - ISPRAVLJENA VERZIJA
// Dodano: FactionCategoryId korištenje pri kreiranju Discord kanala

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Synchronizes Space Engineers factions with Discord roles and channels
    /// Includes faction reading functionality (merged from FactionReaderService)
    /// </summary>
    public class FactionSyncService
    {
        private readonly DatabaseService _db;
        private readonly DiscordService _discord;
        private readonly MainConfig _config;

        /// <summary>
        /// Initialize FactionSyncService with required dependencies
        /// </summary>
        public FactionSyncService(DatabaseService db, DiscordService discord, MainConfig config)
        {
            _db = db;
            _discord = discord;
            _config = config;

            LoggerUtil.LogDebug("[FACTION_SYNC] FactionSyncService initialized");
        }

        /// <summary>
        /// Loads all player-created factions from the current game session.
        /// Filters out NPC factions and factions with non-standard tags.
        /// </summary>
        public List<FactionModel> LoadFactionsFromGame()
        {
            var factionModels = new List<FactionModel>();

            try
            {
                // Access the faction collection from session
                var factionCollection = MySession.Static.Factions as MyFactionCollection;
                if (factionCollection == null)
                {
                    LoggerUtil.LogWarning(
                        "MySession.Static.Factions is null - cannot load factions"
                    );
                    return factionModels;
                }

                // Get all factions from the game
                var allFactions = factionCollection.GetAllFactions();

                if (allFactions == null || allFactions.Length == 0)
                {
                    LoggerUtil.LogInfo("No factions found in session");
                    return factionModels;
                }

                // Iterate through all factions
                foreach (var faction in allFactions)
                {
                    if (faction == null)
                        continue;

                    // Filter: Only 3-character tags (player factions)
                    if (faction.Tag == null || faction.Tag.Length != 3)
                    {
                        continue;
                    }

                    // Filter: Skip NPC factions
                    if (faction.IsEveryoneNpc())
                    {
                        LoggerUtil.LogDebug($"Skipping NPC faction: {faction.Tag}");
                        continue;
                    }

                    // Create faction model
                    var factionModel = new FactionModel
                    {
                        FactionID = (int)faction.FactionId,
                        Tag = faction.Tag,
                        Name = faction.Name ?? "Unknown",
                    };

                    // Load faction members
                    if (faction.Members.Count > 0)
                    {
                        foreach (var memberKvp in faction.Members)
                        {
                            long playerId = memberKvp.Key;
                            var memberData = memberKvp.Value;

                            // Map playerId to SteamID
                            ulong steamId = MyAPIGateway.Players.TryGetSteamId(playerId);

                            if (steamId == 0)
                            {
                                LoggerUtil.LogWarning(
                                    $"Cannot get SteamID for playerId {playerId} in faction {faction.Tag}"
                                );
                                continue;
                            }

                            // Get player name
                            string playerName = GetPlayerName(playerId);

                            // Create faction member model
                            var factionPlayer = new FactionPlayerModel
                            {
                                PlayerID = (int)playerId,
                                SteamID = (long)steamId,
                                OriginalNick = playerName,
                                SyncedNick = playerName,
                            };

                            factionModel.Players.Add(factionPlayer);
                        }
                    }

                    factionModels.Add(factionModel);
                    LoggerUtil.LogDebug(
                        $"Loaded faction: {faction.Tag} ({faction.Name}) with {factionModel.Players.Count} members"
                    );
                }

                LoggerUtil.LogInfo(
                    $"Loaded {factionModels.Count} player factions from game session"
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"Error loading factions from game: {ex.Message}\n{ex.StackTrace}"
                );
            }

            return factionModels;
        }

        /// <summary>
        /// Synchronize all player factions to Discord
        /// Creates Discord roles and channels for each faction
        /// FIXED: Now uses FactionCategoryId when creating channels
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
                        LoggerUtil.LogDebug($"[FACTION_SYNC] Creating Discord role for new faction: {faction.Tag}");
                        faction.DiscordRoleID = await _discord.CreateRoleAsync(faction.Tag);

                        LoggerUtil.LogDebug($"[FACTION_SYNC] Creating Discord channel for new faction: {faction.Name}");
                        // FIXED: Now passes FactionCategoryId to CreateChannelAsync
                        faction.DiscordChannelID = await _discord.CreateChannelAsync(
                            faction.Name.ToLower(),
                            _config.Discord.FactionCategoryId
                        );

                        _db.SaveFaction(faction);
                        LoggerUtil.LogInfo(
                            $"[FACTION_SYNC] Created new faction: {faction.Tag} - {faction.Name} (Discord Role: {faction.DiscordRoleID}, Channel: {faction.DiscordChannelID})"
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
                LoggerUtil.LogError($"[FACTION_SYNC] Sync error: {ex.Message}\n{ex.StackTrace}");
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

        /// <summary>
        /// Retrieves the display name for a player by their identity ID.
        /// </summary>
        private string GetPlayerName(long playerId)
        {
            try
            {
                var identity = MySession.Static.Players.TryGetIdentity(playerId);
                return identity?.DisplayName ?? "Unknown";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning($"Error getting player name for ID {playerId}: {ex.Message}");
                return "Unknown";
            }
        }
    }
}