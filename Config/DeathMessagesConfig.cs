// Config/DeathMessagesConfig.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Config
{
    [Serializable]
    [XmlRoot("DeathMessages")]
    public class DeathMessagesConfig
    {
        [XmlArray("Suicide")]
        [XmlArrayItem("Message")]
        public List<string> SuicideMessages { get; set; } = new List<string>();

        [XmlArray("FirstKill")]
        [XmlArrayItem("Message")]
        public List<string> FirstKillMessages { get; set; } = new List<string>();

        [XmlArray("Retaliate")]
        [XmlArrayItem("Message")]
        public List<string> RetaliateMessages { get; set; } = new List<string>();

        [XmlArray("RetaliateOld")]
        [XmlArrayItem("Message")]
        public List<string> RetaliateOldMessages { get; set; } = new List<string>();

        [XmlArray("Accident")]
        [XmlArrayItem("Message")]
        public List<string> AccidentMessages { get; set; } = new List<string>();

        private static string ConfigPath
        {
            get
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var instanceDir = Path.Combine(baseDir, "Instance");
                var mambaDir = Path.Combine(instanceDir, "mambaTorchDiscordSync");
                return Path.Combine(mambaDir, "DeathMessages.xml");
            }
        }

        public static DeathMessagesConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new DeathMessagesConfig
                {
                    SuicideMessages = new List<string>
                    {
                        "{0} forgot they needed oxygen.",
                        "{0} achieved instant decompression.",
                        "{0} discovered the vacuum's embrace.",
                        "{0} took an unplanned spacewalk.",
                        "{0} forgot to wear a helmet.",
                        "{0} became one with the stars."
                    },
                    FirstKillMessages = new List<string>
                    {
                        "{0} obliterated {1} with their {2} on {3}.",
                        "{1} didn't survive {0}'s {2}.",
                        "{0} introduced {1} to the void using a {2}.",
                        "{1} met their maker courtesy of {0}'s {2}.",
                        "{0} sent {1} to respawn with a {2}."
                    },
                    RetaliateMessages = new List<string>
                    {
                        "{0} got their revenge on {1}.",
                        "{1} paid the price for their actions against {0}.",
                        "{0} settled the score with {1}.",
                        "{1} should've seen {0}'s payback coming.",
                        "{0} remembered what {1} did and responded accordingly."
                    },
                    RetaliateOldMessages = new List<string>
                    {
                        "{0} finally caught up with {1}.",
                        "{1} never expected {0} to remember that old grudge.",
                        "{0} plays the long game. {1} just learned that.",
                        "{1} thought they were safe. {0} disagreed.",
                        "Revenge served cold by {0} against {1}."
                    },
                    AccidentMessages = new List<string>
                    {
                        "{0} didn't expect gravity to be that strong.",
                        "{0} underestimated the asteroid field.",
                        "{0} forgot about the solar flare.",
                        "{0} made friends with a radiation cloud.",
                        "{0} collided with something larger on {1}.",
                        "{0} forgot how to dock properly."
                    }
                };

                defaultConfig.Save();
                return defaultConfig;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(DeathMessagesConfig));
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
            {
                return (DeathMessagesConfig)serializer.Deserialize(fs);
            }
        }

        public void Save()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(DeathMessagesConfig));
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Create))
            {
                serializer.Serialize(fs, this);
            }
        }

        public string GetRandomMessage(string category)
        {
            List<string> messages = category switch
            {
                "Suicide" => SuicideMessages,
                "FirstKill" => FirstKillMessages,
                "Retaliate" => RetaliateMessages,
                "RetaliateOld" => RetaliateOldMessages,
                "Accident" => AccidentMessages,
                _ => new List<string>()
            };

            if (messages.Count == 0) return "{0} died.";
            return messages[new Random().Next(messages.Count)];
        }
    }
}