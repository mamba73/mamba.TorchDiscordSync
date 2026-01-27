// Plugin/MambaTorchDiscordSyncPlugin.cs
using System;
using System.IO;
using Torch;
using Torch.API;
using NLog;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;

namespace mamba.TorchDiscordSync
{
    public class MambaTorchDiscordSyncPlugin : TorchPluginBase
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private Persistent<MainConfig> _configStorage;
        public MainConfig Config => _configStorage?.Data;

        public DiscordService DiscordService { get; private set; }
        public EventLoggingService EventLoggingService { get; private set; }
        public ChatSyncService ChatService { get; private set; }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            // Path: d:\g\torch-server\Instance\mamba.TorchDiscordSync
            string storageDir = Path.Combine(StoragePath, "mamba.TorchDiscordSync");
            if (!Directory.Exists(storageDir)) Directory.CreateDirectory(storageDir);

            string configPath = Path.Combine(storageDir, "Config.xml");

            try
            {
                // Initialize the persistent container
                _configStorage = new Persistent<MainConfig>(configPath, new MainConfig());

                // CRITICAL: Force a reload from disk to ensure we aren't using cached default values
                if (File.Exists(configPath))
                {
                    // This re-reads the XML file into memory
                    _configStorage.Save(); // Ensure the structure is correct
                    Log.Info($"[TDS] Config file found and initialized at: {configPath}");
                }
                else
                {
                    _configStorage.Save();
                    Log.Info($"[TDS] New default config created at: {configPath}");
                }

                DiscordService = new DiscordService(this);
                EventLoggingService = new EventLoggingService(this);
                ChatService = new ChatSyncService(this);

                // Re-verify the loaded token from the current Config object
                string token = Config?.Discord?.BotToken ?? "NULL";
                Log.Info($"[TDS] Config Status: Enabled={Config.Enabled}, Token='{token}'");

                if (Config.Enabled && !string.IsNullOrEmpty(token) && token != "TOKEN")
                {
                    Log.Info("[TDS] Valid token detected. Starting Discord connection...");
                    DiscordService.Start();
                }
                else
                {
                    Log.Warn("[TDS] Startup aborted: Token in memory is still 'TOKEN'.");
                    Log.Warn("[TDS] TIP: Shut down Torch, edit Config.xml, then start Torch again.");
                }

                EventLoggingService.Attach();
                ChatService.Init();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TDS] Critical error during plugin Init.");
            }
        }

        public override void Dispose()
        {
            try
            {
                EventLoggingService?.Detach();
                DiscordService?.Stop();
                ChatService?.Dispose();

                // Save only if we have a valid config object
                if (_configStorage?.Data != null)
                    _configStorage.Save();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TDS] Error during disposal.");
            }
            base.Dispose();
        }
    }
}