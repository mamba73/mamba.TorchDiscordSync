using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    public class FactionSyncService
    {
        private readonly DatabaseService _db;
        private readonly DiscordService _discord;

        public FactionSyncService(DatabaseService db, DiscordService discord)
        {
            _db = db;
            _discord = discord;
        }

        public void SyncFactions(List<FactionModel> factions, List<PlayerModel> players)
        {
            foreach (var faction in factions)
            {
                _db.SaveFaction(faction);
                _discord.SendLog($"Faction {faction.Tag} saved");
            }

            foreach (var player in players)
            {
                var factionTag = factions.Find(f => f.FactionID == player.FactionID)?.Tag ?? "UNK";
                player.SyncedNick = $"[{factionTag}] {player.OriginalNick}";
                _db.SavePlayer(player);
                _discord.SendLog($"Player {player.SyncedNick} saved");
            }
        }
    }
}
