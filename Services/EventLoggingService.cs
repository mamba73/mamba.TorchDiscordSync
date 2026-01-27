using System;
using System.Threading.Tasks;
using Sandbox.Game.World;
using Sandbox.Game;
using NLog;

namespace mamba.TorchDiscordSync.Services
{
    public class EventLoggingService
    {
        private readonly MambaTorchDiscordSyncPlugin _plugin;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public EventLoggingService(MambaTorchDiscordSyncPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Attach()
        {
            MyVisualScriptLogicProvider.PlayerConnected += OnPlayerConnected;
            MyVisualScriptLogicProvider.PlayerDisconnected += OnPlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerDied += OnPlayerDied;
        }

        public void Detach()
        {
            MyVisualScriptLogicProvider.PlayerConnected -= OnPlayerConnected;
            MyVisualScriptLogicProvider.PlayerDisconnected -= OnPlayerDisconnected;
            MyVisualScriptLogicProvider.PlayerDied -= OnPlayerDied;
        }

        private void OnPlayerConnected(long playerId)
        {
            if (!_plugin.Config.Chat.Enabled) return;
            var identity = MySession.Static.Players.TryGetIdentity(playerId);
            if (identity != null)
            {
                string msg = _plugin.Config.Chat.JoinMessage.Replace("{p}", identity.DisplayName);
                _plugin.DiscordService.SendToMainChannel(msg);
            }
        }

        private void OnPlayerDisconnected(long playerId)
        {
            if (!_plugin.Config.Chat.Enabled) return;
            var identity = MySession.Static.Players.TryGetIdentity(playerId);
            if (identity != null)
            {
                string msg = _plugin.Config.Chat.LeaveMessage.Replace("{p}", identity.DisplayName);
                _plugin.DiscordService.SendToMainChannel(msg);
            }
        }

        private void OnPlayerDied(long playerId)
        {
            // Internal Torch event handling
        }

        // Fix for CS1061: Methods expected by SyncOrchestrator and DeathLogService
        public Task LogDeathAsync(string name, string reason = "Unknown")
        {
            Log.Info("[TDS Death] " + name + " died. Reason: " + reason);
            return Task.CompletedTask;
        }

        public Task LogSyncCompleteAsync(int count, int total)
        {
            Log.Info("[TDS Sync] Sync complete. " + count + "/" + total);
            return Task.CompletedTask;
        }

        public Task LogSimSpeedWarningAsync(float speed, string message = null)
        {
            Log.Warn("[TDS Monitor] Sim Speed Drop: " + speed);
            return Task.CompletedTask;
        }

        public Task LogServerStatusAsync(string status, float speed)
        {
            Log.Info("[TDS Status] " + status + " | SimSpeed: " + speed);
            return Task.CompletedTask;
        }

        public Task LogAsync(object message, object status = null)
        {
            Log.Info("[TDS] " + message);
            return Task.CompletedTask;
        }
    }
}