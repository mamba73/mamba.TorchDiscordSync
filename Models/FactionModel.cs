using System;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionModel
    {
        public long FactionID { get; set; }             // SE faction ID
        public string Tag { get; set; }                 // 3-char tag
        public string Name { get; set; }                // Faction name
        public DateTime CreatedAt { get; set; }         // When created in DB
        public DateTime UpdatedAt { get; set; }         // Last sync
        public DateTime? DeletedAt { get; set; }        // Soft delete
    }
}
