// Services/DiscordService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Sandbox;

namespace mamba.TorchDiscordSync.Services
{
    public class DiscordService
    {
        private readonly MambaTorchDiscordSyncPlugin _plugin;
        private DiscordSocketClient _client;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public DiscordService(MambaTorchDiscordSyncPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Start()
        {
            // Run initialization in a separate thread to avoid blocking Torch startup
            Task.Run(async () =>
            {
                try
                {
                    _client = new DiscordSocketClient(new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
                    });

                    // Internal Discord API logging
                    _client.Log += (msg) => { Log.Info($"[TDS Discord API] {msg.Message}"); return Task.CompletedTask; };

                    await _client.LoginAsync(TokenType.Bot, _plugin.Config.Discord.BotToken);
                    await _client.StartAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[TDS] Discord service failed to start.");
                }
            });
        }

        public void Stop()
        {
            if (_client != null)
            {
                Task.Run(async () =>
                {
                    await _client.StopAsync();
                    _client.Dispose();
                }).Wait(5000);
            }
        }

        public void SendToMainChannel(string message) => SendMessageInternal(_plugin.Config.Discord.ChatChannelId, message, "Main");
        public void SendToStaffLog(string message) => SendMessageInternal(_plugin.Config.Discord.StaffLog, message, "Staff");

        private void SendMessageInternal(ulong id, string msg, string name)
        {
            if (_client == null || _client.ConnectionState != ConnectionState.Connected || id == 0) return;

            Task.Run(async () =>
            {
                try
                {
                    // Fixed: Removed null-conditional operator to allow proper awaiting
                    var channel = await _client.GetChannelAsync(id) as IMessageChannel;
                    if (channel != null) await channel.SendMessageAsync(msg);
                }
                catch (Exception ex) { Log.Error(ex, $"[TDS] Failed to send message to {name}"); }
            });
        }

        public async Task<ulong> CreateRoleAsync(string roleName)
        {
            if (_client == null) return 0;
            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null) return 0;

            var role = await guild.CreateRoleAsync(roleName, isMentionable: false);
            return role.Id;
        }

        public async Task<ulong> CreateChannelAsync(string name, ulong roleId = 0)
        {
            if (_client == null) return 0;
            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null) return 0;

            var channel = await guild.CreateTextChannelAsync(name, x =>
            {
                if (_plugin.Config.Discord.FactionCategoryId != 0)
                    x.CategoryId = _plugin.Config.Discord.FactionCategoryId;
            });

            return channel.Id;
        }

        public async Task DeleteRoleAsync(ulong roleId)
        {
            if (_client == null) return;
            var guild = _client.Guilds.FirstOrDefault();
            var role = guild?.GetRole(roleId);
            if (role != null) await role.DeleteAsync();
        }

        public async Task DeleteChannelAsync(ulong channelId)
        {
            if (_client == null) return;
            // Fixed: Check client before awaiting to avoid nullable ValueTask error
            var channel = await _client.GetChannelAsync(channelId) as ITextChannel;
            if (channel != null) await channel.DeleteAsync();
        }

        public Task SendLogAsync(string m, string e = null) { SendToStaffLog(m); return Task.CompletedTask; }
        public Task SendLogAsync(ulong id, string m) { SendMessageInternal(id, m, "Generic Log"); return Task.CompletedTask; }

        private void SafeGameCall(Action action)
        {
            MySandboxGame.Static.Invoke(action, "MambaDiscordSync");
        }
    }
}