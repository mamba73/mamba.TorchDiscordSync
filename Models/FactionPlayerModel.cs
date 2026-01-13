using System;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionPlayerModel
    {
        public long FactionID { get; set; }            // Faction ID
        public ulong PlayerSteamID { get; set; }       // Player SteamID
        public DateTime CreatedAt { get; set; }        // When assigned
        public DateTime UpdatedAt { get; set; }        // Last sync
        public DateTime? DeletedAt { get; set; }       // Soft delete
    }
}
