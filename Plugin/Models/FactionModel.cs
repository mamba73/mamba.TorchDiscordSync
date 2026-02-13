// Plugin/Models/FactionModel.cs
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Plugin.Models
{
    [Serializable]
    public class FactionModel
    {
        [XmlElement]
        public int FactionID { get; set; }

        [XmlElement]
        public string Tag { get; set; }

        [XmlElement]
        public string Name { get; set; }

        [XmlElement]
        public ulong DiscordRoleID { get; set; }

        [XmlElement]
        public ulong DiscordChannelID { get; set; }

        // ========== NOVO: TRACKING POLJA ==========
        [XmlElement]
        public string DiscordRoleName { get; set; }  // = Tag (BLB, sVz)

        [XmlElement]
        public string DiscordChannelName { get; set; }  // = lowercase Name (blind leading blind)

        [XmlElement]
        public string SyncStatus { get; set; } = "Pending";  // Pending, Synced, Failed, Orphaned

        [XmlElement]
        public DateTime? SyncedAt { get; set; }

        [XmlElement]
        public string SyncedBy { get; set; }  // "SyncOrchestrator" ili admin ime

        [XmlElement]
        public string ErrorMessage { get; set; }

        [XmlElement]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [XmlElement]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public List<FactionPlayerModel> Players { get; set; } = new List<FactionPlayerModel>();
    }
}
