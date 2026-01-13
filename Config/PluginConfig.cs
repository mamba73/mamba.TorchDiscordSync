// PluginConfig.cs
namespace mamba.TorchDiscordSync.Config
{
    public class PluginConfig
    {
        public string DiscordToken { get; set; }
        public ulong GuildId { get; set; }
        public int SyncInterval { get; set; } = 60;
        public bool Debug { get; set; } = true;
    }
}
