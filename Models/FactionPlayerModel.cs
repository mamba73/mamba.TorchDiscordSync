using System;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionPlayerModel
    {
        public int FactionId { get; set; }
        public ulong PlayerSteamId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
