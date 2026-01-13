using System;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionPlayerModel
    {
        public long FactionID { get; set; }
        public long SteamID { get; set; }
        public string OriginalNick { get; set; }
        public string SyncedNick { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
