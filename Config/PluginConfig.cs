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

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MambaTorchDiscordSync.cfg");

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
