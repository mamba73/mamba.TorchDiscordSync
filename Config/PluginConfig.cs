using System;
using System.Xml;

namespace mamba.TorchDiscordSync.Config
{
    // Handles plugin configuration stored in XML
    public class PluginConfig
    {
        public string DiscordToken { get; set; }
        public ulong GuildID { get; set; }
        public int SyncIntervalSeconds { get; set; }
        public bool Debug { get; set; }

        private string _filePath;

        public PluginConfig(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public void Load()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(_filePath);

            DiscordToken = doc.SelectSingleNode("/PluginConfig/DiscordToken")?.InnerText;
            GuildID = ulong.Parse(doc.SelectSingleNode("/PluginConfig/GuildID")?.InnerText ?? "0");
            SyncIntervalSeconds = int.Parse(doc.SelectSingleNode("/PluginConfig/SyncIntervalSeconds")?.InnerText ?? "60");
            Debug = bool.Parse(doc.SelectSingleNode("/PluginConfig/Debug")?.InnerText ?? "false");
        }
    }
}
