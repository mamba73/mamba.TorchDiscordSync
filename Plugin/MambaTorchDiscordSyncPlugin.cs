// MambaTorchDiscordSyncPlugin.cs
using System;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using NLog;

namespace mamba.TorchDiscordSync.Plugin
{
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Log.Info("mamba.TorchDiscordSync plugin initialized (stub).");
        }
    }
}
