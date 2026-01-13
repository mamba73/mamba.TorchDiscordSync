using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    public class FactionSyncService
    {
        private readonly DatabaseService _db;

        public FactionSyncService(DatabaseService db)
        {
            _db = db;
        }

        // Example: generate synced nickname
        public string GenerateSyncedNick(string tag, string originalNick)
        {
            return $"[{tag}] {originalNick}";
        }

        // Placeholder for syncing factions → database
        public void SyncFactions(List<FactionModel> factions, List<PlayerModel> players)
        {
            foreach (var player in players)
            {
                player.SyncedNick = GenerateSyncedNick("TAG", player.OriginalNick); // TAG to be fetched per faction
                Console.WriteLine($"[SYNC] Player {player.OriginalNick} → {player.SyncedNick}");
            }
        }
    }
}
