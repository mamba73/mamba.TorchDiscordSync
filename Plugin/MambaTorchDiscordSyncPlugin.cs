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

            // Initialize storage paths for configuration
            string storageDir = Path.Combine(StoragePath, "mamba.TorchDiscordSync");
            if (!Directory.Exists(storageDir)) Directory.CreateDirectory(storageDir);
            string configPath = Path.Combine(storageDir, "Config.xml");

            try
            {
                // Load persistent configuration from XML
                _configStorage = new Persistent<MainConfig>(configPath, new MainConfig());
                if (!File.Exists(configPath)) _configStorage.Save();

                // Service instantiation
                DiscordService = new DiscordService(this);
                EventLoggingService = new EventLoggingService(this);
                ChatService = new ChatSyncService(this);

                // Verification log for the loaded token to assist in debugging
                string token = Config.Discord?.BotToken ?? "NULL";
                Log.Info($"[TDS] Config Status: Enabled={Config.Enabled}, Token='{token}'");

                // Conditional startup based on configuration validity
                if (Config.Enabled && !string.IsNullOrEmpty(token) && token != "TOKEN")
                {
                    Log.Info("[TDS] Bot token detected. Attempting Discord connection...");
                    DiscordService.Start();
                }
                else
                {
                    Log.Warn("[TDS] Startup skipped. Verify Token in Config.xml.");
                }

                EventLoggingService.Attach();
                ChatService.Init();
                Log.Info("[TDS] Plugin initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TDS] Critical error during plugin initialization.");
            }
        }

        public override void Dispose()
        {
            try
            {
                // Clean up all resources and detach event handlers
                EventLoggingService?.Detach();
                DiscordService?.Stop();
                ChatService?.Dispose();
                _configStorage?.Save();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TDS] Error during plugin disposal.");
            }
            base.Dispose();
        }
    }
}