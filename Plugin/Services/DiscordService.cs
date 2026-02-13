// Plugin/Services/DiscordService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Utils;

namespace mamba.TorchDiscordSync.Plugin.Services
{
    /// <summary>
    /// Wrapper/Adapter for DiscordBotService
    /// Provides simplified interface for other services
    /// All methods delegate to DiscordBotService
    /// </summary>
    public class DiscordService
    {
        private readonly DiscordBotService _botService;
        private readonly mamba.TorchDiscordSync.Plugin.Config.DiscordConfig _discordConfig;

        public DiscordService(DiscordBotService botService)
        {
            _botService = botService;
            MainConfig cfg = MainConfig.Load();
            _discordConfig =
                cfg != null
                    ? cfg.Discord
                    : new mamba.TorchDiscordSync.Plugin.Config.DiscordConfig();
        }

        /// <summary>
        /// Send message to Discord channel
        /// </summary>
        public async Task<bool> SendLogAsync(ulong channelID, string message)
        {
            try
            {
                if (channelID == 0)
                    return false;

                if (_botService != null)
                {
                    return await _botService.SendChannelMessageAsync(channelID, message);
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Send log error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Create Discord role for faction
        /// </summary>
        public async Task<ulong> CreateRoleAsync(string roleName)
        {
            try
            {
                if (_botService != null)
                {
                    return await _botService.CreateRoleAsync(roleName, null);
                }
                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Create role error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Create Discord text channel for faction
        /// FIXED: Now accepts 3 parameters: channelName, categoryID, roleID
        /// </summary>
        public async Task<ulong> CreateChannelAsync(
            string channelName,
            ulong? factionCategoryId = null,
            ulong? roleID = null
        )
        {
            try
            {
                LoggerUtil.LogInfo("[DISCORD] Creating channel: " + channelName);
                if (_botService != null)
                    return await _botService.CreateChannelAsync(
                        channelName,
                        factionCategoryId,
                        roleID
                    );
                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Create channel error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Create voice channel in faction category with same role permissions. Name = lowercase.</summary>
        public async Task<ulong> CreateVoiceChannelAsync(string channelName, ulong? categoryID = null, ulong? roleID = null)
        {
            try
            {
                if (_botService != null)
                    return await _botService.CreateVoiceChannelAsync(channelName, categoryID, roleID);
                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Create voice channel error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Create forum channel in faction category with same role permissions. Name = lowercase.</summary>
        public async Task<ulong> CreateForumChannelAsync(string channelName, ulong? categoryID = null, ulong? roleID = null)
        {
            try
            {
                if (_botService != null)
                    return await _botService.CreateForumChannelAsync(channelName, categoryID, roleID);
                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Create forum channel error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Delete Discord role
        /// </summary>
        public async Task<bool> DeleteRoleAsync(ulong roleID)
        {
            try
            {
                if (_botService != null)
                {
                    return await _botService.DeleteRoleAsync(roleID);
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Delete role error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Delete Discord channel
        /// </summary>
        public async Task<bool> DeleteChannelAsync(ulong channelID)
        {
            try
            {
                LoggerUtil.LogInfo("[DISCORD] Deleting channel: " + channelID);
                if (_botService != null)
                    return await _botService.DeleteChannelAsync(channelID);
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Delete channel error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// NEW: Check if a role already exists by ID
        /// Returns the role if found, null otherwise
        /// </summary>
        public IRole GetExistingRole(ulong roleId)
        {
            try
            {
                if (_botService == null)
                {
                    LoggerUtil.LogWarning("[DISCORD] Bot service is null");
                    return null;
                }

                // Get the Discord guild from the bot service
                var guild = _botService.GetGuild();
                if (guild == null)
                {
                    LoggerUtil.LogWarning("[DISCORD] Guild not found while checking for role");
                    return null;
                }

                // Try to get the role by ID
                var role = guild.GetRole(roleId);
                if (role != null)
                {
                    LoggerUtil.LogDebug(
                        "[DISCORD] Found existing role: " + role.Name + " (ID: " + roleId + ")"
                    );
                }
                else
                {
                    LoggerUtil.LogDebug("[DISCORD] Role not found (ID: " + roleId + ")");
                }

                return role;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning("[DISCORD] Error checking for existing role: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// NEW: Check if a channel already exists by ID
        /// Returns the channel if found, null otherwise
        /// </summary>
        public async Task<IChannel> GetExistingChannelAsync(ulong channelId)
        {
            try
            {
                if (_botService == null)
                {
                    LoggerUtil.LogWarning("[DISCORD] Bot service is null");
                    return null;
                }

                // Get the Discord client from the bot service
                var client = _botService.GetClient();
                if (client == null)
                {
                    LoggerUtil.LogWarning("[DISCORD] Client not found while checking for channel");
                    return null;
                }

                // FIXED: Use GetChannelAsync method from Discord client
                var channel = await client.GetChannelAsync(channelId);
                if (channel != null)
                {
                    LoggerUtil.LogDebug(
                        "[DISCORD] Found existing channel: "
                            + channel.Name
                            + " (ID: "
                            + channelId
                            + ")"
                    );
                }
                else
                {
                    LoggerUtil.LogDebug("[DISCORD] Channel not found (ID: " + channelId + ")");
                }

                return channel;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning(
                    "[DISCORD] Error checking for existing channel: " + ex.Message
                );
                return null;
            }
        }

        /// <summary>
        /// NEW: Synchronous version of GetExistingChannel for backward compatibility
        /// </summary>
        public IChannel GetExistingChannel(ulong channelId)
        {
            try
            {
                // Run the async method synchronously
                return GetExistingChannelAsync(channelId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning(
                    "[DISCORD] Error checking for existing channel (sync): " + ex.Message
                );
                return null;
            }
        }

        /// <summary>
        /// Get underlying bot service
        /// Use when you need direct access to DiscordBotService
        /// </summary>
        public DiscordBotService GetBotService()
        {
            return _botService;
        }

        /// <summary>
        /// Check if service is connected
        /// </summary>
        public bool IsConnected
        {
            get { return _botService != null && _botService.IsConnected; }
        }

        /// <summary>
        /// Check if service is ready
        /// </summary>
        public bool IsReady
        {
            get { return _botService != null && _botService.IsReady; }
        }

        /// <summary>
        /// Send status message to status channel
        /// </summary>
        public void SendMessage(string message)
        {
            try
            {
                if (
                    _discordConfig != null
                    && _discordConfig.StatusChannelId != 0
                    && _botService != null
                )
                {
                    var _ = _botService.SendChannelMessageAsync(
                        _discordConfig.StatusChannelId,
                        message
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Send message error: " + ex.Message);
            }
        }

        /// <summary>
        /// Assign a role to a Discord user
        /// Used for verification role assignment
        /// </summary>
        public async Task<bool> AssignRoleToUserAsync(ulong userId, ulong roleId)
        {
            try
            {
                LoggerUtil.LogDebug($"[DISCORD_ROLE] Assigning role {roleId} to user {userId}");

                if (_botService != null)
                {
                    bool result = await _botService.AssignRoleAsync(userId, roleId);

                    if (result)
                    {
                        LoggerUtil.LogSuccess(
                            $"[DISCORD_ROLE] Successfully assigned role {roleId} to user {userId}"
                        );
                    }
                    else
                    {
                        LoggerUtil.LogWarning(
                            $"[DISCORD_ROLE] Failed to assign role {roleId} to user {userId}"
                        );
                    }

                    return result;
                }

                LoggerUtil.LogError("[DISCORD_ROLE] Bot service is null");
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[DISCORD_ROLE] Assign role error: {ex.Message}");
                return false;
            }
        }

        internal object SendDirectMessage(long playerSteamID, string v)
        {
            throw new NotImplementedException();
        }
    }
}
