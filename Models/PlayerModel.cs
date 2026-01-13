
// PlayerModel.cs
namespace mamba.TorchDiscordSync.Models
{
    public class PlayerModel
    {
        public long PlayerId { get; set; } // PlayerID in SE
        public string Name { get; set; }
        public ulong SteamId { get; set; }
    }
}