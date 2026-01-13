using System;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    // Placeholder for Discord API integration
    public class DiscordService
    {
        public void SendLog(string message)
        {
            Console.WriteLine("[DISCORD LOG] " + message);
        }

        public void UpdateNickname(PlayerModel player, string tag)
        {
            player.SyncedNick = "[" + tag + "] " + player.OriginalNick;
            SendLog("Updated nick for " + player.OriginalNick + " -> " + player.SyncedNick);
        }
    }
}
