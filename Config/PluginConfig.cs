namespace mamba.TorchDiscordSync.Config
{
    public class PluginConfig
    {
        public bool Debug { get; set; } = true;             // Enable verbose logging
        public int SyncIntervalSeconds { get; set; } = 60; // Frequency of SE → DB sync
        public string DbFile { get; set; } = "data/se.db"; // SQLite file path
        // Discord settings for future
        public string DiscordToken { get; set; }
        public ulong GuildID { get; set; }
        public ulong CategoryID { get; set; }
    }
}
