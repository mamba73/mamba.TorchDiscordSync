using System;

namespace mamba.TorchDiscordSync.Services
{
    public class DiscordService
    {
        private readonly string _token;
        private readonly ulong _guildId;

        public DiscordService() { }

        public DiscordService(string token, ulong guildId)
        {
            _token = token;
            _guildId = guildId;
        }

        public void SendLog(string message)
        {
            Console.WriteLine("[DISCORD] " + message);
        }
    }
}
