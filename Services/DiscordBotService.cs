// Services/DiscordBotService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    public class DiscordBotService
    {
        private readonly DiscordBotConfig _config;
        private DiscordSocketClient _client;
        private bool _isConnected = false;
        private bool _isReady = false;

        public DiscordBotService(DiscordBotConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Initialize and connect Discord bot
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_isConnected)
                    return true;

                LoggerUtil.LogInfo("[DISCORD_BOT] Initializing Discord bot...");

                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.DirectMessages |
                                     GatewayIntents.Guilds |
                                     GatewayIntents.GuildMessages |
                                     GatewayIntents.MessageContent
                };

                _client = new DiscordSocketClient(config);

                _client.Ready += OnBotReady;
                _client.Disconnected += OnBotDisconnected;
                _client.MessageReceived += OnMessageReceived;
                _client.UserJoined += OnUserJoined;

                await _client.LoginAsync(TokenType.Bot, _config.BotToken);
                await _client.StartAsync();

                _isConnected = true;
                LoggerUtil.LogSuccess("[DISCORD_BOT] Bot connection established");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Connection failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Disconnect bot
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_client != null)
                {
                    await _client.LogoutAsync();
                    await _client.StopAsync();
                    _client.Dispose();
                }

                _isConnected = false;
                _isReady = false;
                LoggerUtil.LogInfo("[DISCORD_BOT] Bot disconnected");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Disconnect error: " + ex.Message);
            }
        }

        /// <summary>
        /// Send DM to Discord user with verification instructions
        /// </summary>
        public async Task<bool> SendVerificationDMAsync(string discordUsername, string verificationCode)
        {
            try
            {
                if (!_isReady)
                {
                    LoggerUtil.LogError("[DISCORD_BOT] Bot not ready, cannot send DM");
                    return false;
                }

                var user = FindUserByUsername(discordUsername);
                if (user == null)
                {
                    LoggerUtil.LogWarning("[DISCORD_BOT] User not found: " + discordUsername);
                    return false;
                }

                var dmChannel = await user.CreateDMChannelAsync();
                if (dmChannel == null)
                {
                    LoggerUtil.LogWarning("[DISCORD_BOT] Could not open DM with " + discordUsername);
                    return false;
                }

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("🔐 Space Engineers Verification Request")
                    .WithDescription("Someone has requested to link your Discord account to a Space Engineers account.")
                    .AddField("Verification Code", "```" + verificationCode + "```", false)
                    .AddField("How to Complete", "React to this message or type:\n" + _config.BotPrefix + "verify " + verificationCode, false)
                    .AddField("⏱️ Expires", "This code will expire in " + _config.VerificationCodeExpirationMinutes + " minutes", false)
                    .WithFooter("If you didn't request this, simply ignore this message")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await dmChannel.SendMessageAsync(embed: embed);

                LoggerUtil.LogSuccess("[DISCORD_BOT] Sent verification DM to " + discordUsername);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Send DM error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send success notification DM
        /// </summary>
        public async Task<bool> SendVerificationSuccessDMAsync(string discordUsername, string playerName, long steamID)
        {
            try
            {
                if (!_isReady)
                    return false;

                var user = FindUserByUsername(discordUsername);
                if (user == null)
                    return false;

                var dmChannel = await user.CreateDMChannelAsync();
                if (dmChannel == null)
                    return false;

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ Verification Successful!")
                    .WithDescription("Your Discord account has been successfully linked to Space Engineers.")
                    .AddField("Game Player", playerName, true)
                    .AddField("Steam ID", steamID.ToString(), true)
                    .AddField("✨ You can now use:", "Faction channels, death notifications, chat sync", false)
                    .WithFooter("Welcome to the server!")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await dmChannel.SendMessageAsync(embed: embed);

                LoggerUtil.LogSuccess("[DISCORD_BOT] Sent success DM to " + discordUsername);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Send success DM error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send message to Discord channel
        /// </summary>
        public async Task<bool> SendChannelMessageAsync(ulong channelID, string message)
        {
            try
            {
                if (!_isReady)
                    return false;

                var channel = _client.GetChannel(channelID) as IMessageChannel;
                if (channel == null)
                {
                    LoggerUtil.LogWarning("[DISCORD_BOT] Channel not found: " + channelID);
                    return false;
                }

                await channel.SendMessageAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Send channel message error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send embed to Discord channel
        /// </summary>
        public async Task<bool> SendEmbedAsync(ulong channelID, Embed embed)
        {
            try
            {
                if (!_isReady)
                    return false;

                var channel = _client.GetChannel(channelID) as IMessageChannel;
                if (channel == null)
                    return false;

                await channel.SendMessageAsync(embed: embed);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Send embed error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Create a Discord role
        /// </summary>
        public async Task<ulong> CreateRoleAsync(string roleName, Color? color = null)
        {
            try
            {
                if (!_isReady)
                    return 0;

                var guild = _client.GetGuild(_config.GuildID);
                if (guild == null)
                {
                    LoggerUtil.LogError("[DISCORD_BOT] Guild not found");
                    return 0;
                }

                var role = await guild.CreateRoleAsync(roleName, color: color);
                LoggerUtil.LogSuccess("[DISCORD_BOT] Created role: " + roleName);
                return role.Id;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Create role error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Delete a Discord role
        /// </summary>
        public async Task<bool> DeleteRoleAsync(ulong roleID)
        {
            try
            {
                if (!_isReady)
                    return false;

                var guild = _client.GetGuild(_config.GuildID);
                if (guild == null)
                    return false;

                var role = guild.GetRole(roleID);
                if (role == null)
                    return false;

                await role.DeleteAsync();
                LoggerUtil.LogSuccess("[DISCORD_BOT] Deleted role: " + roleID);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Delete role error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Assign role to user
        /// </summary>
        public async Task<bool> AssignRoleAsync(ulong userID, ulong roleID)
        {
            try
            {
                if (!_isReady)
                    return false;

                var guild = _client.GetGuild(_config.GuildID);
                if (guild == null)
                    return false;

                var user = guild.GetUser(userID);
                if (user == null)
                    return false;

                var role = guild.GetRole(roleID);
                if (role == null)
                    return false;

                await user.AddRoleAsync(role);
                LoggerUtil.LogSuccess("[DISCORD_BOT] Assigned role " + roleID + " to user " + userID);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Assign role error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Remove role from user
        /// </summary>
        public async Task<bool> RemoveRoleAsync(ulong userID, ulong roleID)
        {
            try
            {
                if (!_isReady)
                    return false;

                var guild = _client.GetGuild(_config.GuildID);
                if (guild == null)
                    return false;

                var user = guild.GetUser(userID);
                if (user == null)
                    return false;

                await user.RemoveRoleAsync(guild.GetRole(roleID));
                LoggerUtil.LogSuccess("[DISCORD_BOT] Removed role " + roleID + " from user " + userID);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Remove role error: " + ex.Message);
                return false;
            }
        }

        public bool IsReady { get { return _isReady; } }
        public bool IsConnected { get { return _isConnected; } }

        // ============================================================
        // PRIVATE EVENT HANDLERS
        // ============================================================

        private async Task OnBotReady()
        {
            _isReady = true;
            LoggerUtil.LogSuccess("[DISCORD_BOT] Bot is ready and listening!");
            await Task.CompletedTask;
        }

        private async Task OnBotDisconnected(Exception ex)
        {
            _isReady = false;
            string exMsg = ex != null ? ex.Message : "Unknown error";
            LoggerUtil.LogWarning("[DISCORD_BOT] Bot disconnected: " + exMsg);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handle incoming messages (DM and guild messages)
        /// </summary>
        private async Task OnMessageReceived(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot)
                    return;

                if (!message.Content.StartsWith(_config.BotPrefix))
                    return;

                var args = message.Content.Substring(_config.BotPrefix.Length).Split(new char[] { ' ' });
                var command = args[0].ToLower();

                if (command == "verify")
                {
                    await HandleVerifyCommand(message, args);
                }
                else if (command == "help")
                {
                    await HandleHelpCommand(message);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Message handler error: " + ex.Message);
            }

            await Task.CompletedTask;
        }

        private async Task HandleVerifyCommand(SocketMessage message, string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    await message.Author.SendMessageAsync("❌ Usage: !verify CODE\n\nExample: !verify ABC12345");
                    return;
                }

                string code = args[1].ToUpper();

                OnVerificationAttempt?.Invoke(code, message.Author.Id, message.Author.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("⏳ Verifying...")
                    .WithDescription("Your verification code is being processed.")
                    .WithFooter("You will receive a confirmation message shortly")
                    .Build();

                await message.Author.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Verify command error: " + ex.Message);
            }
        }

        private async Task HandleHelpCommand(SocketMessage message)
        {
            try
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("🤖 mamba.TorchDiscordSync Bot Help")
                    .AddField("Verification", _config.BotPrefix + "verify CODE - Verify your Space Engineers account", false)
                    .AddField("Help", _config.BotPrefix + "help - Show this message", false)
                    .WithFooter("Bot will respond to you via DM")
                    .Build();

                await message.Author.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Help command error: " + ex.Message);
            }
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            try
            {
                LoggerUtil.LogInfo("[DISCORD_BOT] New user joined: " + user.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTitle("👋 Welcome!")
                    .WithDescription("Welcome to the Space Engineers community!")
                    .AddField("Link Your Account", "If you play on our SE server, use `/tds verify @YourDiscordName` in-game", false)
                    .AddField("Need Help?", "Type `!help` to learn more", false)
                    .Build();

                await user.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] User joined handler error: " + ex.Message);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Find Discord user by username
        /// </summary>
        private SocketUser FindUserByUsername(string username)
        {
            try
            {
                var guild = _client.GetGuild(_config.GuildID);
                if (guild == null)
                {
                    LoggerUtil.LogError("[DISCORD_BOT] Guild not found");
                    return null;
                }

                foreach (var user in guild.Users)
                {
                    if (user.Username == username || user.Nickname == username)
                        return user;
                }

                foreach (var user in guild.Users)
                {
                    if (user.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                        (user.Nickname != null && user.Nickname.Equals(username, StringComparison.OrdinalIgnoreCase)))
                        return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD_BOT] Find user error: " + ex.Message);
                return null;
            }
        }

        internal void SendMessage(object serverStartedMessage)
        {
            throw new NotImplementedException();
        }

        // ============================================================
        // PUBLIC EVENTS
        // ============================================================

        public event Action<string, ulong, string> OnVerificationAttempt;
    }
}