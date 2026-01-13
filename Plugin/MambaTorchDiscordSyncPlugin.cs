using System;
using System.Collections.Generic;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Config;

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

            _config = new PluginConfig("PluginConfig.xml");
            _db = new DatabaseService("data/se.db");
            _discord = new DiscordService();
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

            var players = new List<PlayerModel>
            {
                new PlayerModel { SteamID = 12345678901234567, OriginalNick = "mamba" }
            };

            var factions = new List<FactionModel>
            {
                new FactionModel { FactionID = 1, Tag = "BLB", Name = "Blind Leading Blind" }
            };

            _syncService.SyncFactions(factions, players);
        }
    }
}
