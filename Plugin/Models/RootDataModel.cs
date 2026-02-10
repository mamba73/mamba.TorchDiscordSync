// Plugin/Models/RootDataModel.cs
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Plugin.Models
{
    [XmlRoot("MambaTorchDiscordSyncData")]
    public class RootDataModel
    {
        [XmlArray("Factions")]
        [XmlArrayItem("Faction")]
        public List<FactionModel> Factions { get; set; } = new List<FactionModel>();

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();

        [XmlArray("EventLogs")]
        [XmlArrayItem("Event")]
        public List<EventLogModel> EventLogs { get; set; } = new List<EventLogModel>();

        [XmlArray("DeathHistory")]
        [XmlArrayItem("Death")]
        public List<DeathHistoryModel> DeathHistory { get; set; } = new List<DeathHistoryModel>();

        // NEW: Verification arrays
        [XmlArray("Verifications")]
        [XmlArrayItem("Verification")]
        public List<VerificationModel> Verifications { get; set; } = new List<VerificationModel>();

        [XmlArray("VerificationHistory")]
        [XmlArrayItem("Entry")]
        public List<VerificationHistoryModel> VerificationHistory { get; set; } = new List<VerificationHistoryModel>();
    }
}
