// Core/VerificationCommandHandler.cs
using System;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Core
{
    public class VerificationCommandHandler
    {
        private readonly MainConfig _config;
        private readonly VerificationService _verification;
        private readonly EventLoggingService _eventLog;
        private readonly DiscordBotService _discordBot;
        private readonly DiscordBotConfig _discordBotConfig;

        public VerificationCommandHandler(VerificationService verification, EventLoggingService evtLog, MainConfig config, DiscordBotService bot, DiscordBotConfig botConfig)
        {
            _verification = verification;
            _eventLog = evtLog;
            _config = config;
            _discordBot = bot;
            _discordBotConfig = botConfig;  
        }

        /// <summary>
        /// Handle in-game /tds verify @DiscordUsername command
        /// Generates verification code and sends DM to Discord user
        /// </summary>
        public async Task<string> HandleVerifyCommandAsync(long playerSteamID,
            string playerName, string discordUsername)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(discordUsername))
                    return "Error: Discord username is required. Usage: /tds verify @DiscordUsername";

                discordUsername = discordUsername.TrimStart('@').Trim();

                if (discordUsername.Length < 2 || discordUsername.Length > 32)
                    return "Error: Invalid Discord username length (2-32 characters)";

                string code = _verification.GenerateVerificationCode(playerSteamID, playerName, discordUsername);

                if (code == null)
                    return "Error: You already have a pending verification code. It expires in 15 minutes.";

                bool dmSent = await _discordBot.SendVerificationDMAsync(discordUsername, code);

                if (!dmSent)
                {
                    return "Error: Could not find Discord user '" + discordUsername + "' or send DM.\n" +
                           "Make sure:\n" +
                           "  - Username is correct\n" +
                           "  - User is in the Discord server\n" +
                           "  - Bot has DM permissions";
                }

                await _eventLog.LogAsync("VerificationRequest",
                    playerName + " (" + playerSteamID + ") requested verification as " + discordUsername + ". DM sent.");

                string message = "Verification code sent to " + discordUsername + " via DM!\n" +
                                "Check your Discord private messages\n" +
                                "Code expires in " + _discordBotConfig.VerificationCodeExpirationMinutes + " minutes";

                LoggerUtil.LogInfo("[VERIFY] " + playerName + ": " + discordUsername + " - Code: " + code + " - DM sent");
                return message;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Command error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Handle verification from Discord bot (!verify CODE command)
        /// Called when user completes verification on Discord
        /// </summary>
        public Task<string> VerifyFromDiscordAsync(string code, ulong discordId, string discordUsername)
        {
            if (string.IsNullOrEmpty(code))
            {
                return Task.FromResult("Invalid verification code");
            }

            if (_verification != null)
            {
                return _verification.VerifyAsync(code, discordId, discordUsername);
            }

            return Task.FromResult("Verification service unavailable");
        }

        /// <summary>
        /// Handle /tds unverify STEAMID [reason] command (admin only)
        /// Removes verification link for a player
        /// </summary>
        public async Task<string> HandleUnverifyCommandAsync(long steamID, string reason = "Admin removal")
        {
            try
            {
                bool success = _verification.RemoveVerification(steamID, reason);

                if (success)
                {
                    await _eventLog.LogAsync("VerificationRemoved",
                        "SteamID " + steamID + " unverified: " + reason);

                    LoggerUtil.LogSuccess("[VERIFY] Unverified SteamID " + steamID + ": " + reason);
                    return "Verification removed";
                }
                else
                {
                    return "Error: Verification not found for this Steam ID";
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Unverify error: " + ex.Message);
                return "Error: " + ex.Message;
            }
        }
    }
}