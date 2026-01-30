// Models/DeathHistoryModel.cs
using System;

namespace mamba.TorchDiscordSync.Models
{
    [System.Serializable]
    public class DeathHistoryModel
    {
        public long KillerSteamID { get; set; }
        public long VictimSteamID { get; set; }
        public DateTime DeathTime { get; set; }
        public string DeathType { get; set; }
        public object[] KillerName { get; internal set; }
        public string VictimName { get; internal set; }
    }
}