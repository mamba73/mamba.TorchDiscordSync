using System;
using System.Collections.Generic;
using NLog; // <-- dodano
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Plugin
{
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(); // <-- definirano

        private DatabaseService _db;
        private FactionSyncService _syncService;
        private DiscordService _discord;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            _db = new DatabaseService("data/se.db");
            _syncService = new FactionSyncService(_db);
            _discord = new DiscordService();

            var sessionManager = torch.Managers.GetManager<ITorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += OnSessionStateChanged;

            Log.Info("MambaTorchDiscordSyncPlugin loaded"); // <-- sada radi
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            if (state != TorchSessionState.Loaded) return;

            Console.WriteLine("[PLUGIN] Session loaded, performing initial faction sync");
            Log.Info("[PLUGIN] Session loaded, performing initial faction sync"); // opcionalno

            // Example stub: Normally you would read SE factions here
            var players = new List<PlayerModel>
            {
                new PlayerModel { SteamID = 12345678901234567, OriginalNick = "mamba" }
            };

            var factions = new List<FactionModel>
            {
                new FactionModel { FactionID = 1, Tag = "BLB", Name = "Blind Leading Blind" }
            };

            _syncService.SyncFactions(factions, players);

            // Log to Discord placeholder
            foreach (var player in players)
                _discord.SendLog($"Synced {player.SyncedNick}");
        }
    }
}
