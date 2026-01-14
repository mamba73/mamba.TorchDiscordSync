using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    public class DatabaseService
    {
        private readonly string _xmlPath;
        public List<FactionModel> Factions { get; set; } = new List<FactionModel>();

        public DatabaseService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var instanceDir = Path.Combine(baseDir, "Instance");
            if (!Directory.Exists(instanceDir))
                Directory.CreateDirectory(instanceDir);

            var folder = Path.Combine(instanceDir, "mambaTorchDiscordSync");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _xmlPath = Path.Combine(folder, "MambaTorchDiscordSync.xml");

            if (File.Exists(_xmlPath))
                LoadFromXml();
        }

        public void LoadFromXml()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<FactionModel>), new XmlRootAttribute("Factions"));
            using (FileStream fs = new FileStream(_xmlPath, FileMode.Open))
            {
                Factions = (List<FactionModel>)serializer.Deserialize(fs);
            }
        }

        public void SaveToXml()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<FactionModel>), new XmlRootAttribute("Factions"));
            using (FileStream fs = new FileStream(_xmlPath, FileMode.Create))
            {
                serializer.Serialize(fs, Factions);
            }
        }

        public void SaveFaction(FactionModel faction)
        {
            var existing = Factions.FirstOrDefault(f => f.FactionID == faction.FactionID);
            if (existing != null)
            {
                existing.Tag = faction.Tag;
                existing.Name = faction.Name;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Players = faction.Players;
            }
            else
            {
                faction.CreatedAt = DateTime.UtcNow;
                faction.UpdatedAt = DateTime.UtcNow;
                Factions.Add(faction);
            }

            SaveToXml();
        }

        public void SavePlayer(FactionPlayerModel player, int factionID)
        {
            var faction = Factions.FirstOrDefault(f => f.FactionID == factionID);
            if (faction == null) return;

            var existing = faction.Players.FirstOrDefault(p => p.PlayerID == player.PlayerID);
            if (existing != null)
            {
                existing.OriginalNick = player.OriginalNick;
                existing.SyncedNick = player.SyncedNick;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                player.CreatedAt = DateTime.UtcNow;
                player.UpdatedAt = DateTime.UtcNow;
                faction.Players.Add(player);
            }

            SaveToXml();
        }
    }
}
