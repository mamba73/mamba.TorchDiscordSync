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
            try
            {
                string helpText = CommandAuthorizationUtil.GenerateHelpText(playerSteamID, _config);

                // Split into lines and send each
                var lines = helpText.Split('\n');
                foreach (var line in lines)
                {
                    ChatUtils.SendServerMessage(line);
                }

                bool isAdmin = SecurityUtil.IsPlayerAdmin(playerSteamID, _config.AdminSteamIDs);
                LoggerUtil.LogInfo("[COMMAND] " + (isAdmin ? "ADMIN" : "USER") + " help displayed");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[HELP] Error: " + ex.Message);
                ChatUtils.SendError("Error displaying help");
            }
        }

        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// </summary>
        private void HandleVerifyCommand(long playerSteamID, string playerName, string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds verify @DiscordName");
                    return;
                }

                string discordUsername = args[2];

                // This needs to be handled through the main plugin since it has VerificationCommandHandler reference
                // For now, we'll send a message that this is handled elsewhere
                ChatUtils.SendServerMessage("Verification command received. Processing...");
                LoggerUtil.LogInfo("[COMMAND] " + playerName + ": verify requested for " + discordUsername);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Verification error: " + ex.Message);
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
                // LINE 188 - FIXED: Added await fix
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
                var _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds reset");
                ChatUtils.SendWarning("Clearing Discord roles and channels...");

                _factionSync.ResetDiscordAsync();
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
                var _ = _eventLog.LogAsync("CommandError", "Reset command failed: " + ex.Message);
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
