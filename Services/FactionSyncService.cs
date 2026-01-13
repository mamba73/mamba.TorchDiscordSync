using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    // Handles syncing factions with Discord and database
    public class FactionSyncService
    {
        private DatabaseService _db;
        private DiscordService _discord;

        public FactionSyncService(DatabaseService db, DiscordService discord = null)
        {
            _db = db;
            _discord = discord ?? new DiscordService();
        }

        public void SyncFactions(List<FactionModel> factions, List<PlayerModel> players)
        {
            foreach (var faction in factions)
            {
                faction.CreatedAt = DateTime.UtcNow;
                faction.UpdatedAt = DateTime.UtcNow;
                _db.SaveFaction(faction);

                foreach (var player in players)
                {
                    player.CreatedAt = DateTime.UtcNow;
                    player.UpdatedAt = DateTime.UtcNow;
                    _discord.UpdateNickname(player, faction.Tag);
                    _db.SavePlayer(player);
                }
            }
        }
    }
}
