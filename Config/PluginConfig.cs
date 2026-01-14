using System;
using System.IO;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Config
{
    [Serializable]
    public class PluginConfig
    {
        public string DiscordToken { get; set; }
        public ulong GuildID { get; set; }
        public int SyncIntervalSeconds { get; set; }
        public bool Debug { get; set; }

        // Folder unutar Instance
        private static string ConfigDir
        {
            get
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var instanceDir = Path.Combine(baseDir, "Instance");
                if (!Directory.Exists(instanceDir))
                    Directory.CreateDirectory(instanceDir);

                var mambaDir = Path.Combine(instanceDir, "mambaTorchDiscordSync");
                if (!Directory.Exists(mambaDir))
                    Directory.CreateDirectory(mambaDir);

                return mambaDir;
            }
        }

        private static string ConfigPath => Path.Combine(ConfigDir, "MambaTorchDiscordSync.cfg");

        public static PluginConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new PluginConfig
                {
                    DiscordToken = "YOUR_DISCORD_BOT_TOKEN",
                    GuildID = 0,
                    SyncIntervalSeconds = 60,
                    Debug = true
                };

                defaultConfig.Save();
                return defaultConfig;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(PluginConfig));
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
            {
                return (PluginConfig)serializer.Deserialize(fs);
            }
        }

        public void Save()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PluginConfig));
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Create))
            {
                serializer.Serialize(fs, this);
            }
        }
    }
}
