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

        public void SyncFactions(List<FactionModel> factions)
        {
            var factionsCopy = new List<FactionModel>(factions);

            foreach (var faction in factionsCopy)
            {
                _db.SaveFaction(faction);
                _discord.SendLog($"Faction {faction.Tag} saved");

                foreach (var player in faction.Players)
                {
                    player.SyncedNick = $"[{faction.Tag}] {player.OriginalNick}";
                    _db.SavePlayer(player, faction.FactionID);
                    _discord.SendLog($"Player {player.SyncedNick} saved");
                }
            }
        }
    }
}
