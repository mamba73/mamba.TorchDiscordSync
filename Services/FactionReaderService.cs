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
    /// Service responsible for reading faction data from the game session
    /// and converting it to FactionModel objects for Discord synchronization.
    /// </summary>
    public class FactionReaderService
    {
        /// <summary>
        /// Loads all player-created factions from the current game session.
        /// Filters out NPC factions and only includes factions with 3-character tags.
        /// </summary>
        /// <returns>List of FactionModel objects representing player factions</returns>
        public List<FactionModel> LoadFactionsFromGame()
        {
            var factionModels = new List<FactionModel>();

            try
            {
                // Access the faction collection from the game session
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
                    LoggerUtil.LogInfo("No factions found in game session");
                    return factionModels;
                }

                LoggerUtil.LogInfo($"Processing {allFactions.Length} factions from game session");

                foreach (var faction in allFactions)
                {
                    try
                    {
                        // Filter: only 3-character tags (player factions)
                        if (string.IsNullOrEmpty(faction.Tag) || faction.Tag.Length != 3)
                        {
                            continue;
                        }

                        // Filter: skip NPC factions
                        if (faction.IsEveryoneNpc())
                        {
                            LoggerUtil.LogDebug($"Skipping NPC faction: {faction.Tag}");
                            continue;
                        }

                        // Create faction model
                        var factionModel = new FactionModel
                        {
                            FactionID = faction.FactionId,
                            Tag = faction.Tag,
                            Name = faction.Name ?? faction.Tag
                        };

                        // Load faction members
                        if (faction.Members != null)
                        {
                            foreach (var memberKvp in faction.Members)
                            {
                                try
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

                                    var factionPlayer = new FactionPlayerModel
                                    {
                                        SteamID = (long)steamId, // Convert ulong to long for XML serialization
                                        PlayerName = playerName,
                                        IsLeader = memberData.IsLeader,
                                        IsFounder = memberData.IsFounder
                                    };

                                    factionModel.Members.Add(factionPlayer);
                                }
                                catch (Exception ex)
                                {
                                    LoggerUtil.LogError($"Error processing faction member in {faction.Tag}: {ex.Message}");
                                }
                            }
                        }

                        factionModels.Add(factionModel);
                        LoggerUtil.LogDebug($"Loaded faction: {faction.Tag} ({faction.Name}) with {factionModel.Members.Count} members");
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError($"Error processing faction {faction?.Tag}: {ex.Message}");
                    }
                }

                LoggerUtil.LogInfo($"Successfully loaded {factionModels.Count} player factions from game");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error loading factions from game: {ex.Message}");
            }

            return factionModels;
        }

        /// <summary>
        /// Gets the display name of a player by their identity ID.
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
                LoggerUtil.LogError($"Error getting player name for ID {playerId}: {ex.Message}");
                return "Unknown";
            }
        }
    }
}
