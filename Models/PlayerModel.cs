using System;

namespace mamba.TorchDiscordSync.Models
{
    public class PlayerModel
    {
        public ulong SteamId { get; set; }
        public string OriginalNick { get; set; }
        public string SyncedNick { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
