using System;

namespace mamba.TorchDiscordSync.Models
{
    public class PlayerModel
    {
        public long SteamID { get; set; }
        public string OriginalNick { get; set; }
        public string SyncedNick { get; set; }
        public int FactionID { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
