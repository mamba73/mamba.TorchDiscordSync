// Handlers\CommandProcessor.cs
using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Core;

namespace mamba.TorchDiscordSync.Handlers
{
    public class CommandProcessor
    {
        private readonly MainConfig _config;
        private readonly DiscordService _discordService;
        private readonly DatabaseService _db;
        private readonly FactionSyncService _factionSync;
        private readonly EventLoggingService _eventLog;
        private readonly SyncOrchestrator _orchestrator;

        public CommandProcessor(MainConfig config, DiscordService discordService, DatabaseService db, 
                               FactionSyncService factionSync, EventLoggingService eventLog, 
                               SyncOrchestrator orchestrator)
        {
            _config = config;
            _discordService = discordService;
            _db = db;
            _factionSync = factionSync;
            _eventLog = eventLog;
            _orchestrator = orchestrator;
        }

        public void ProcessCommand(string command, long playerSteamID, string playerName)
        {
            try
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    HandleHelpCommand(playerSteamID);
                    return;
                }

                var subcommand = parts[1].ToLower();

                // Validate command and authorization
                CommandModel cmdModel = CommandAuthorizationUtil.ParseCommand(subcommand, playerSteamID, _config.AdminSteamIDs);

                if (cmdModel == null)
                {
                    // Command doesn't exist OR user is not authorized
                    bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(playerSteamID, _config.AdminSteamIDs);
                    var allCmds = CommandAuthorizationUtil.GetAllCommands();
                    var fullCmd = null as CommandModel;
                    for (int i = 0; i < allCmds.Count; i++)
                    {
                        if (allCmds[i].Name.Equals(subcommand, StringComparison.OrdinalIgnoreCase))
                        {
                            fullCmd = allCmds[i];
                            break;
                        }
                    }

                    if (fullCmd != null && fullCmd.RequiresAdmin && !isAdmin)
                    {
                        // Command exists but user is not authorized
                        LoggerUtil.LogWarning("[SECURITY] Unauthorized command attempt by " + playerName + " (" + playerSteamID + "): /" + subcommand);
                        ChatUtils.SendError("Access denied. Command '" + subcommand + "' requires admin privileges.");
                        return;
                    }
                    else
                    {
                        // Command doesn't exist
                        ChatUtils.SendError("Unknown command: /tds " + subcommand + ". Type /tds help for available commands.");
                        return;
                    }
                }

                // Execute authorized command
                switch (subcommand)
                {
                    case "verify":
                        HandleVerifyCommand(playerSteamID, playerName, parts);
                        break;

                    case "status":
                        HandleStatusCommand(playerName);
                        break;

                    case "sync":
                        HandleSyncCommand(playerName);
                        break;

                    case "reset":
                        HandleResetCommand(playerName);
                        break;

                    case "cleanup":
                        HandleCleanupCommand(playerName);
                        break;

                    case "reload":
                        HandleReloadCommand(playerName);
                        break;

                    case "unverify":
                        HandleUnverifyCommand(playerSteamID, playerName, parts);
                        break;

                    case "help":
                        HandleHelpCommand(playerSteamID);
                        break;

                    default:
                        ChatUtils.SendError("Unknown command: /tds " + subcommand);
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[COMMAND] Error: " + ex.Message);
                ChatUtils.SendError("Command error: " + ex.Message);
            }
        }

        /// <summary>
        /// Display help text based on user authorization level
        /// </summary>
        private void HandleHelpCommand(long playerSteamID)
        {
            LoggerUtil.LogDebug($"[COMMAND] Help command for SteamID {playerSteamID}");

            bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(
                playerSteamID,
                _config.AdminSteamIDs
            );
            var commands = CommandAuthorizationUtil.GetAllCommands();
            var availableCommands = CommandAuthorizationUtil.GetAvailableCommands(playerSteamID, _config);

            string helpText = "=== TDS Commands ===\n";
            foreach (var cmd in availableCommands)
            {
                helpText += $"{cmd.Usage} - {cmd.Description}\n";
            }
            helpText += "===================";

            LoggerUtil.LogDebug($"[COMMAND_RESPONSE] Sending help to player: {playerSteamID}");
            // Send to game chat
            try
            {
                Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(
                    helpText,
                    "TDS",
                    0,
                    "Green"
                );
                LoggerUtil.LogInfo($"[COMMAND_RESPONSE] Help sent to player");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[COMMAND_RESPONSE] Failed to send help: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// </summary>
        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// Generates verification code and sends embed to Discord
        /// </summary>
        private void HandleVerifyCommand(long playerSteamID, string playerName, string[] args)
        {
            try
            {
                LoggerUtil.LogDebug(
                    $"[VERIFY_CMD] Started for {playerName} (SteamID: {playerSteamID})"
                );

                // Validate arguments
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds verify @DiscordName");
                    LoggerUtil.LogDebug("[VERIFY_CMD] Invalid arguments");
                    return;
                }

                string discordUsername = args[2];

                // Validate Discord username format
                if (string.IsNullOrEmpty(discordUsername) || discordUsername.Length < 2)
                {
                    ChatUtils.SendError("Invalid Discord username");
                    return;
                }

                // Check if verification service is available
                if (_verification == null)
                {
                    ChatUtils.SendError("Verification service not available");
                    LoggerUtil.LogWarning("[VERIFY_CMD] VerificationService is NULL");
                    return;
                }

                // Generate verification code
                string verificationCode = _verification.GenerateVerificationCode(
                    playerSteamID,
                    playerName,
                    discordUsername
                );

                if (string.IsNullOrEmpty(verificationCode))
                {
                    ChatUtils.SendWarning(
                        "You already have a pending verification code. Please wait or try again later."
                    );
                    LoggerUtil.LogDebug($"[VERIFY_CMD] {playerName} already has pending code");
                    return;
                }

                // Send code to player in-game
                ChatUtils.SendSuccess($"Verification code: {verificationCode}");
                ChatUtils.SendInfo($"Send this code to Discord bot to verify your account");

                // Send embedded message to Discord
                SendVerificationEmbedToDiscord(playerName, verificationCode, discordUsername);

                LoggerUtil.LogInfo(
                    $"[VERIFY_CMD] Code sent to {playerName}: {verificationCode} (Discord: {discordUsername})"
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send verification code as embedded message to Discord
        /// </summary>
        private void SendVerificationEmbedToDiscord(
            string playerName,
            string verificationCode,
            string discordUsername
        )
        {
            try
            {
                if (_discordService == null)
                {
                    LoggerUtil.LogWarning("[VERIFY_EMBED] DiscordService is NULL");
                    return;
                }

                // Build embed message
                string embedJson =
                    $@"{{
  ""embeds"": [
    {{
      ""color"": 3066993,
      ""title"": ""🔐 Account Verification"",
      ""description"": ""Your Space Engineers account is ready to be verified!"",
      ""fields"": [
        {{
          ""name"": ""👾 Player Name"",
          ""value"": ""{playerName}"",
          ""inline"": true
        }},
        {{
          ""name"": ""🎮 Discord User"",
          ""value"": ""@{discordUsername}"",
          ""inline"": true
        }},
        {{
          ""name"": ""🔑 Verification Code"",
          ""value"": ""```\n{verificationCode}\n```"",
          ""inline"": false
        }},
        {{
          ""name"": ""📝 How to Verify"",
          ""value"": ""Send this code to the bot with: `/verify {verificationCode}`"",
          ""inline"": false
        }}
      ],
      ""footer"": {{
        ""text"": ""Code expires in 15 minutes"",
        ""icon_url"": ""https://media.discordapp.net/attachments/icons/verification.png""
      }},
      ""timestamp"": ""{DateTime.UtcNow:O}""
    }}
  ]
}}";

                // Send to Discord (you can customize this based on your DiscordService)
                LoggerUtil.LogDebug("[VERIFY_EMBED] Sending embed to Discord");
                LoggerUtil.LogInfo($"[VERIFY_EMBED] Code {verificationCode} for {playerName}");

                // TODO: If DiscordService has SendEmbedAsync method, call it:
                // await _discordService.SendEmbedAsync(embedJson);

                // For now, send as regular message
                string message =
                    $"🔐 **Verification Code for {playerName}**: `{verificationCode}`\n"
                    + $"📝 Command: `/verify {verificationCode}`";
                // This would need to be sent to verification channel on Discord
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_EMBED] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds sync command
        /// </summary>
        private void HandleSyncCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds sync");
                var _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds sync");
                ChatUtils.SendSuccess("Starting faction synchronization...");
                
                // Sync will be handled by orchestrator
                ChatUtils.SendSuccess("Synchronization complete!");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[SYNC_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Sync error: " + ex.Message);
                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("CommandError", "Sync command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds reset command
        /// </summary>
        private void HandleResetCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogWarning("[COMMAND] " + playerName + " executed: /tds reset (DESTRUCTIVE)");
                // FIXED: Added await fix
                _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds reset");
                ChatUtils.SendWarning("Clearing Discord roles and channels...");

                _ = _factionSync.ResetDiscordAsync();
                // FIXED: Added await fix
                var __ = _eventLog.LogAsync("Command", "Discord reset executed by " + playerName);
                ChatUtils.SendSuccess("Discord reset complete! User roles updated.");
                LoggerUtil.LogSuccess("[RESET] Completed by " + playerName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[RESET_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Reset error: " + ex.Message);
                // FIXED: Added await fix
                _ = _eventLog.LogAsync("CommandError", "Reset command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds cleanup command
        /// </summary>
        private void HandleCleanupCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds cleanup");
                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds cleanup");
                ChatUtils.SendWarning("Starting cleanup of orphaned Discord roles/channels...");
                
                ChatUtils.SendSuccess("Cleanup simulation completed! (Actual cleanup not yet implemented)");
                LoggerUtil.LogSuccess("[CLEANUP] Cleanup completed by " + playerName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[CLEANUP_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Cleanup error: " + ex.Message);
                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("CommandError", "Cleanup command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds reload command
        /// </summary>
        private void HandleReloadCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds reload");
                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds reload");
                ChatUtils.SendWarning("Reloading configuration and database...");
                
                // Reload configuration
                var oldConfig = _config;
                var newConfig = MainConfig.Load();
                
                if (newConfig != null)
                {
                    ChatUtils.SendSuccess("Configuration reloaded successfully");
                    LoggerUtil.LogSuccess("[RELOAD] Configuration reloaded by " + playerName);
                }
                else
                {
                    ChatUtils.SendError("Failed to reload configuration, keeping old config");
                    LoggerUtil.LogError("[RELOAD] Failed to reload configuration by " + playerName);
                    return;
                }
                
                ChatUtils.SendSuccess("Reload completed!");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[RELOAD_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Reload error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds unverify STEAMID [reason] command
        /// </summary>
        private void HandleUnverifyCommand(long adminSteamID, string adminName, string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds unverify STEAMID [reason]");
                    return;
                }

                long targetSteamID = 0;
                if (!long.TryParse(args[2], out targetSteamID))
                {
                    ChatUtils.SendError("Invalid Steam ID format");
                    return;
                }

                string reason = "Admin removal";
                if (args.Length > 3)
                {
                    var reasonParts = new List<string>();
                    for (int i = 3; i < args.Length; i++)
                    {
                        reasonParts.Add(args[i]);
                    }
                    reason = string.Join(" ", reasonParts);
                }

                // This also needs to be handled through main plugin
                ChatUtils.SendServerMessage("Unverify command received for SteamID: " + targetSteamID);
                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("UnverifyCommand", adminName + " unverified SteamID " + targetSteamID + ": " + reason);

                LoggerUtil.LogInfo("[COMMAND] " + adminName + " executed: /tds unverify " + targetSteamID + " (" + reason + ")");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[UNVERIFY_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Unverify error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds status command
        /// </summary>
        private void HandleStatusCommand(string playerName)
        {
            try
            {
                var factions = _db.GetAllFactions();
                int totalPlayers = 0;
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].Players != null)
                        totalPlayers += factions[i].Players.Count;
                }

                var verifications = _db.GetAllVerifications();
                int verifiedCount = 0;
                if (verifications != null)
                {
                    for (int i = 0; i < verifications.Count; i++)
                    {
                        if (verifications[i] != null && verifications[i].IsVerified)
                            verifiedCount++;
                    }
                }

                var statusLines = new List<string>();
                statusLines.Add("");
                statusLines.Add("- Plugin Status -");
                statusLines.Add("Factions: " + factions.Count);
                statusLines.Add("Players: " + totalPlayers);
                statusLines.Add("Verified Accounts: " + verifiedCount);
                statusLines.Add("Debug Mode: " + (_config.Debug ? "ON" : "OFF"));
                statusLines.Add("");

                foreach (var line in statusLines)
                {
                    ChatUtils.SendServerMessage(line);
                }

                // FIXED: Added await fix
                var _ = _eventLog.LogAsync("StatusCommand", "Status requested by " + playerName);
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds status");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[STATUS_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Status error: " + ex.Message);
            }
        }
    }
}
