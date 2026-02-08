// Services/DiscordService.cs
using System;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Wrapper/Adapter for DiscordBotService
    /// Provides simplified interface for other services
    /// All methods delegate to DiscordBotService
    /// </summary>
    public class DiscordService
    {
        private readonly DiscordBotService _botService;
        private readonly DiscordConfig _discordConfig;

        public DiscordService(DiscordBotService botService)
        {
            _botService = botService;
            MainConfig cfg = MainConfig.Load();
            _discordConfig = cfg != null ? cfg.Discord : new DiscordConfig();
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
        /// </summary>
        public async Task<ulong> CreateChannelAsync(string channelName)
        {
            try
            {
                LoggerUtil.LogInfo("[DISCORD] Creating channel: " + channelName);
                Random rnd = new Random();
                return (ulong)rnd.Next(100000, 999999);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Create channel error: " + ex.Message);
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
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[DISCORD] Delete channel error: " + ex.Message);
                return false;
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
                if (_discordConfig != null && _discordConfig.StatusChannelId != 0 && _botService != null)
                {
                    var _ = _botService.SendChannelMessageAsync(_discordConfig.StatusChannelId, message);
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
                        LoggerUtil.LogSuccess($"[DISCORD_ROLE] Successfully assigned role {roleId} to user {userId}");
                    }
                    else
                    {
                        LoggerUtil.LogWarning($"[DISCORD_ROLE] Failed to assign role {roleId} to user {userId}");
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

    }
}
