using System;

namespace mamba.TorchDiscordSync.Models
{
    public class PlayerModel
    {
        public ulong SteamID { get; set; }              // SteamID of the player
        public string OriginalNick { get; set; }        // Original nickname in SE
        public string SyncedNick { get; set; }          // Nickname after sync [TAG] OriginalNick
        public DateTime CreatedAt { get; set; }         // When record was created
        public DateTime UpdatedAt { get; set; }         // Last updated
        public DateTime? DeletedAt { get; set; }        // Soft delete
    }
}
