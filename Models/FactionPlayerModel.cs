using System;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionPlayerModel
    {
        public int PlayerID { get; set; }
        public long SteamID { get; set; }
        public string OriginalNick { get; set; }
        public string SyncedNick { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
