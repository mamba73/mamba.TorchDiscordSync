// Services/ChatSyncService.cs
using System;
using Torch.API.Managers;
using NLog;

namespace mamba.TorchDiscordSync.Services
{
    public class ChatSyncService
    {
        private readonly MambaTorchDiscordSyncPlugin _plugin;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public ChatSyncService(MambaTorchDiscordSyncPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Init()
        {
            // Subscribe to Torch chat manager events for message relay
            var chatManager = _plugin.Torch.CurrentSession?.Managers.GetManager<IChatManagerServer>();
            if (chatManager != null)
            {
                chatManager.MessageProcessing += OnChatMessage;
                Log.Info("[TDS] ChatSyncService initialized.");
            }
        }

        private void OnChatMessage(TorchChatMessage msg, ref bool consumed)
        {
            // Exit early if syncing is disabled or message is a command
            if (!_plugin.Config.Chat.Enabled || !_plugin.Config.Chat.ServerToDiscord) return;
            if (msg.Message.StartsWith("/")) return;

            try
            {
                // Process message formatting safely on a background task
                string formatted = _plugin.Config.Chat.GameToDiscordFormat
                    .Replace("{p}", msg.Author)
                    .Replace("{msg}", msg.Message);

                _plugin.DiscordService.SendToMainChannel(formatted);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TDS] Error during chat message processing.");
            }
        }

        public void Dispose()
        {
            // Unsubscribe from events to prevent memory leaks and duplicate messages
            var chatManager = _plugin.Torch.CurrentSession?.Managers.GetManager<IChatManagerServer>();
            if (chatManager != null)
            {
                chatManager.MessageProcessing -= OnChatMessage;
            }
        }
    }
}