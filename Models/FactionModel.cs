// FactionModel.cs
using System.Collections.Generic;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionModel
    {
        public int FactionId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public bool IsPlayerFaction { get; set; }
        public List<ulong> MembersSteamIds { get; set; } = new List<ulong>();
    }
}

