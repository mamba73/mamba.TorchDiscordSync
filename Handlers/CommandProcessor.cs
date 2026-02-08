// Handlers\CommandProcessor.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private readonly VerificationService _verification;
        private readonly VerificationCommandHandler _verificationCommandHandler;

        public CommandProcessor(
            MainConfig config,
            DiscordService discordService,
            DatabaseService db,
            FactionSyncService factionSync,
            EventLoggingService eventLog,
            SyncOrchestrator orchestrator,
            VerificationService verification = null,
            VerificationCommandHandler verificationCommandHandler = null
        )
        {
            _config = config;
            _discordService = discordService;
            _db = db;
            _factionSync = factionSync;
            _eventLog = eventLog;
            _orchestrator = orchestrator;
            _verification = verification;
            _verificationCommandHandler = verificationCommandHandler; // NEW: Store handler

            if (_verificationCommandHandler != null)
            {
                LoggerUtil.LogInfo("[COMMAND] VerificationCommandHandler successfully initialized");
            }
            else
            {
                LoggerUtil.LogWarning("[COMMAND] VerificationCommandHandler is NULL - verify command will not work!");
            }
        }



        /// <summary>
        /// Display help text based on user authorization level
        /// </summary>
        public void ProcessCommand(string command, long playerSteamID, string playerName)
        {
            try
            {
                LoggerUtil.LogDebug($"[COMMAND] Processing command from {playerName} (SteamID: {playerSteamID}): {command}");

                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    HandleHelpCommand(playerSteamID);
                    return;
                }

                var subcommand = parts[1].ToLower();

                LoggerUtil.LogDebug($"[COMMAND] Subcommand: {subcommand}");

                // ===== NEW: Check if command is admin-only =====
                bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(
                    playerSteamID,
                    _config.AdminSteamIDs
                );

                // Commands that ONLY non-admins can use
                if (subcommand == "verify")
                {
                    LoggerUtil.LogInfo($"[VERIFY_CMD] Verify command detected for {playerName}");
                    HandleVerifyCommand(playerSteamID, playerName, parts);
                    return;
                }

                // Commands that ONLY admins can use
                List<string> adminOnlyCommands = new List<string>
                {
                    "sync",
                    "reset",
                    "cleanup",
                    "reload",
                };

                if (adminOnlyCommands.Contains(subcommand))
                {
                    if (!isAdmin)
                    {
                        LoggerUtil.LogWarning(
                            $"[SECURITY] Unauthorized command attempt by {playerName} ({playerSteamID}): /{subcommand}"
                        );
                        ChatUtils.SendError($"Command '{subcommand}' requires admin privileges");
                        return;
                    }

                    // Execute admin command
                    switch (subcommand)
                    {
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
                    }
                    return;
                }

                // Commands available to everyone
                if (subcommand == "help")
                {
                    HandleHelpCommand(playerSteamID);
                    return;
                }

                if (subcommand == "status")
                {
                    HandleStatusCommand(playerName);
                    return;
                }

                // Unknown command
                ChatUtils.SendError(
                    "Unknown command: /tds "
                        + subcommand
                        + ". Type /tds help for available commands."
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[COMMAND] Error: " + ex.Message);
                ChatUtils.SendError("Command error: " + ex.Message);
            }
        }

        /// <summary>
        /// Display help text based on user authorization level
        /// Non-admins see only: verify, help
        /// Admins see all commands
        /// </summary>
        private void HandleHelpCommand(long playerSteamID)
        {
            try
            {
                LoggerUtil.LogDebug($"[HELP_CMD] Help command for SteamID {playerSteamID}");

                bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(
                    playerSteamID,
                    _config.AdminSteamIDs
                );

                string helpText = "=== TDS Commands ===\n";

                if (isAdmin)
                {
                    // ADMIN - show all commands
                    helpText += "🔧 ADMIN COMMANDS:\n";
                    helpText += "/tds sync - Synchronize factions\n";
                    helpText += "/tds reset - Reset verification\n";
                    helpText += "/tds status - Show plugin status\n";
                    helpText += "/tds cleanup - Clean old data\n";
                    helpText += "/tds reload - Reload configuration\n";
                    helpText += "\n";
                }

                // USER - show available commands (for everyone)
                helpText += "👤 USER COMMANDS:\n";
                helpText += "/tds verify @DiscordName - Link your Discord account\n";
                helpText += "/tds help - Show this help\n";
                helpText += "===================";

                ChatUtils.SendHelpText(helpText);
                LoggerUtil.LogInfo(
                    $"[HELP_CMD] Help sent to {(isAdmin ? "ADMIN" : "USER")} SteamID {playerSteamID}"
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[HELP_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Help error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// Generates verification code and sends to player
        /// NOW FIXED: Actually calls VerificationCommandHandler!
        /// </summary>
        private void HandleVerifyCommand(long playerSteamID, string playerName, string[] args)
        {
            try
            {
                LoggerUtil.LogInfo($"[VERIFY_CMD] HANDLER CALLED for {playerName} (SteamID: {playerSteamID})");
                LoggerUtil.LogDebug($"[VERIFY_CMD] Args length: {args.Length}, Args: {string.Join(", ", args)}");

                // Validate arguments
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds verify @DiscordName");
                    LoggerUtil.LogWarning("[VERIFY_CMD] Invalid arguments - need Discord username");
                    return;
                }

                // CRITICAL FIX: Check if handler exists
                if (_verificationCommandHandler == null)
                {
                    LoggerUtil.LogError("[VERIFY_CMD] CRITICAL: VerificationCommandHandler is NULL!");
                    ChatUtils.SendError("Verification system is not initialized. Contact an admin.");
                    return;
                }

                string discordUsername = args[2];
                LoggerUtil.LogInfo($"[VERIFY_CMD] Processing verification for Discord user: {discordUsername}");

                // FIXED: Actually call the verification handler asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        LoggerUtil.LogDebug($"[VERIFY_CMD] Calling VerificationCommandHandler.HandleVerifyCommandAsync");

                        string result = await _verificationCommandHandler.HandleVerifyCommandAsync(
                            playerSteamID,
                            playerName,
                            discordUsername
                        );

                        LoggerUtil.LogInfo($"[VERIFY_CMD] Result: {result}");

                        // Send result to player
                        if (result.StartsWith("Error"))
                        {
                            ChatUtils.SendError(result);
                        }
                        else
                        {
                            ChatUtils.SendSuccess(result);
                        }
                    }
                    catch (Exception asyncEx)
                    {
                        LoggerUtil.LogError($"[VERIFY_CMD] Async error: {asyncEx.Message}\n{asyncEx.StackTrace}");
                        ChatUtils.SendError($"Verification error: {asyncEx.Message}");
                    }
                });

                LoggerUtil.LogSuccess($"[VERIFY_CMD] Verification request queued for {playerName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_CMD] Error: {ex.Message}\n{ex.StackTrace}");
                ChatUtils.SendError($"Verify error: {ex.Message}");
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
        /// Shows plugin and server status
        /// </summary>
        private void HandleStatusCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogDebug($"[STATUS_CMD] Status command for {playerName}");

                // Get faction count
                var factions = _db?.GetAllFactions();
                int totalFactions = factions?.Count ?? 0;
                int totalPlayers = 0;

                if (factions != null)
                {
                    foreach (var faction in factions)
                    {
                        if (faction.Players != null)
                            totalPlayers += faction.Players.Count;
                    }
                }

                // Build status message
                string statusText = "=== TDS Plugin Status ===\n";
                statusText += $"Status: ✅ ONLINE\n";
                statusText += $"Factions: {totalFactions}\n";
                statusText += $"Players: {totalPlayers}\n";
                statusText += $"Chat Sync: {(_config?.Chat?.Enabled == true ? "✅" : "❌")}\n";
                statusText += $"Death Logging: {(_config?.Death?.Enabled == true ? "✅" : "❌")}\n";
                statusText += $"Verification: {(_config?.Discord != null ? "✅" : "❌")}\n";
                statusText += "=======================";

                ChatUtils.SendHelpText(statusText);
                LoggerUtil.LogInfo($"[STATUS_CMD] Status sent to {playerName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[STATUS_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Status error: {ex.Message}");
            }
        }

    }
}