using System;
using System.Collections.Generic;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Services;

namespace mamba.TorchDiscordSync.Plugin
{
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        private DatabaseService _db;
        private FactionSyncService _syncService;
        private DiscordService _discord;
        private PluginConfig _config;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            _config = PluginConfig.Load();
            _db = new DatabaseService();
            _discord = new DiscordService(_config.DiscordToken, _config.GuildID);
            _syncService = new FactionSyncService(_db, _discord);

            var sessionManager = torch.Managers.GetManager<ITorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += OnSessionStateChanged;

            Console.WriteLine("[PLUGIN] MambaTorchDiscordSyncPlugin loaded");
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            if (state != TorchSessionState.Loaded) return;

            Console.WriteLine("[PLUGIN] Session loaded, performing initial faction sync");

            var factions = new List<FactionModel>
            {
                new FactionModel
                {
                    FactionID = 1,
                    Tag = "BLB",
                    Name = "Blind Leading Blind",
                    Players = new List<FactionPlayerModel>
                    {
                        new FactionPlayerModel
                        {
                            PlayerID = 1001,
                            SteamID = 12345678901234567,
                            OriginalNick = "mamba"
                        }
                    }
                }
            };

            _syncService.SyncFactions(factions);
        }
    }
}
