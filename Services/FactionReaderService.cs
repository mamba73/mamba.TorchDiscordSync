// Services/FactionReaderService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service responsible for reading faction data from the game session.
    /// Filters player-created factions (3-character tags) and maps members to Steam IDs.
    /// </summary>
    public class FactionReaderService
    {
        /// <summary>
        /// Loads all player-created factions from the current game session.
        /// Filters out NPC factions and factions with non-standard tags.
        /// </summary>
        /// <returns>List of FactionModel objects representing player factions</returns>
        public List<FactionModel> LoadFactionsFromGame()
        {
            var factionModels = new List<FactionModel>();

            try
            {
                // Access the faction collection from session
                var factionCollection = MySession.Static.Factions as MyFactionCollection;
                if (factionCollection == null)
                {
                    LoggerUtil.LogWarning("MySession.Static.Factions is null - cannot load factions");
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
                    if (faction == null) continue;

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
                        FactionID = (int)faction.FactionId,  // long to int conversion
                        Tag = faction.Tag,
                        Name = faction.Name ?? "Unknown"
                    };

                    // Load faction members
                    // DictionaryReader doesn't support null check - check Count instead
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
                                LoggerUtil.LogWarning($"Cannot get SteamID for playerId {playerId} in faction {faction.Tag}");
                                continue;
                            }

                            // Get player name
                            string playerName = GetPlayerName(playerId);

                            // Create faction member model
                            var factionPlayer = new FactionPlayerModel
                            {
                                PlayerID = (int)playerId,  // long to int conversion
                                SteamID = (long)steamId,   // ulong to long for XML serialization
                                OriginalNick = playerName,
                                SyncedNick = playerName
                            };

                            factionModel.Players.Add(factionPlayer);
                        }
                    }

                    factionModels.Add(factionModel);
                    LoggerUtil.LogDebug($"Loaded faction: {faction.Tag} ({faction.Name}) with {factionModel.Players.Count} members");
                }

                LoggerUtil.LogInfo($"Loaded {factionModels.Count} player factions from game session");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error loading factions from game: {ex.Message}\n{ex.StackTrace}");
            }

            return factionModels;
        }

        /// <summary>
        /// Retrieves the display name for a player by their identity ID.
        /// </summary>
        /// <param name="playerId">The player's identity ID</param>
        /// <returns>Player display name or "Unknown" if not found</returns>
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