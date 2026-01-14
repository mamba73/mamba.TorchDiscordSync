using System.Collections.Generic;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Models
{
    [XmlRoot("MambaTorchDiscordSyncData")]
    public class RootDataModel
    {
        [XmlArray("Factions")]
        [XmlArrayItem("Faction")]
        public List<FactionModel> Factions { get; set; } = new List<FactionModel>();
    }
}
