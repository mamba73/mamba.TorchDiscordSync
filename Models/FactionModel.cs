// Models/FactionModel.cs
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Models
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

        [XmlElement]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [XmlElement]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public List<FactionPlayerModel> Players { get; set; } = new List<FactionPlayerModel>();
    }
}
