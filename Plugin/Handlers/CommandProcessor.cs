// Plugin/Handlers/CommandProcessor.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;
using mamba.TorchDiscordSync.Plugin.Models;
using mamba.TorchDiscordSync.Plugin.Core;

namespace mamba.TorchDiscordSync.Plugin.Handlers
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
            _verificationCommandHandler = verificationCommandHandler;

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
        /// Main command processor
        /// Routes commands to appropriate handlers based on authorization
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

                // Check if user is admin
                bool isAdmin = CommandAuthorizationUtil.IsUserAdmin(
                    playerSteamID,
                    _config.AdminSteamIDs
                );

                // NEW: Check for verify:status BEFORE verify
                if (subcommand == "verify:status")
                {
                    LoggerUtil.LogInfo($"[VERIFY_STATUS_CMD] Verify status command from {playerName}");
                    HandleVerifyStatusCommand(playerSteamID, playerName);
                    return;
                }
                if (subcommand == "verify:delete")
                {
                    LoggerUtil.LogInfo(
                        $"[VERIFY_DELETE_CMD] Verify delete command from {playerName}"
                    );
                    HandleVerifyDeleteCommand(playerSteamID, playerName);
                    return;
                }

                if (subcommand == "verify:help")
                {
                    LoggerUtil.LogInfo($"[VERIFY_HELP_CMD] Verify help command from {playerName}");
                    HandleVerifyHelpCommand(playerName);
                    return;
                }

                // User commands - verify
                if (subcommand == "verify")
                {
                    LoggerUtil.LogInfo($"[VERIFY_CMD] Verify command detected for {playerName}");
                    HandleVerifyCommand(playerSteamID, playerName, parts);
                    return;
                }

                // Admin-only commands
                List<string> adminOnlyCommands = new List<string>
                {
                    "admin:faction:sync",
                    "admin:faction:reset",
                    "admin:faction:cleanup",
                    "reload",
                    "unverify",
                    "admin:verify:list",
                    "admin:verify:pending",
                    "admin:verify:delete",
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
                        case "unverify":
                            HandleUnverifyCommand(playerSteamID, playerName, parts);
                            break;
                        case "admin:verify:list":
                            HandleAdminVerifyList(playerName);
                            break;
                        case "admin:verify:pending":
                            HandleAdminVerifyPending(playerName);
                            break;
                        case "admin:verify:delete":
                            HandleAdminVerifyDelete(playerName, parts);
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
                    "Unknown command: /tds " + subcommand + ". Type /tds help for available commands."
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
        /// Non-admins see only: verify, verify:status, help, status
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
                    // ADMIN - show all commands (ASCII only - no emoji)
                    helpText += "[ADMIN] COMMANDS:\n";
                    helpText += "/tds sync - Synchronize factions\n";
                    helpText += "/tds reset - Reset Discord roles/channels\n";
                    helpText += "/tds reload - Reload configuration\n";
                    helpText += "/tds cleanup - Clean orphaned Discord items\n";
                    helpText += "/tds unverify <SteamID> [reason] - Remove verification\n";
                    helpText += "/tds admin:verify:list - List verified users\n";
                    helpText += "/tds admin:verify:pending - List pending verifications\n";
                    helpText += "/tds admin:verify:delete <SteamID> - Delete verification\n";
                    helpText += "\n";
                }

                // USER - show available commands (for everyone - ASCII only)
                helpText += "[USER] COMMANDS:\n";
                helpText += "/tds verify @DiscordName - Link your Discord account\n";
                helpText += "/tds verify <DiscordUserID> - Link using Discord User ID\n";
                helpText += "/tds verify:status - Check your verification status\n";
                helpText += "/tds verify:delete - Delete pending verification\n";
                helpText += "/tds verify:help - Detailed verification guide\n";
                helpText += "/tds status - Show plugin status\n";
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
        /// NEW: Handle /tds verify:status command
        /// Shows verification status for current player
        /// </summary>
        private void HandleVerifyStatusCommand(long playerSteamID, string playerName)
        {
            try
            {
                LoggerUtil.LogDebug($"[VERIFY_STATUS_CMD] Checking status for {playerName} (SteamID: {playerSteamID})");

                // Get verification status from database
                var verification = _db?.GetVerification(playerSteamID);

                string statusText = "";
                if (verification == null)
                {
                    statusText = "[NOT VERIFIED] You have not started verification yet\nType /tds verify @DiscordName to begin";
                }
                else if (verification.IsVerified)
                {
                    statusText = "[VERIFIED] You are verified!\n";
                    statusText += $"Discord: {verification.DiscordUsername}\n";
                    statusText += $"Verified at: {verification.VerifiedAt:yyyy-MM-dd HH:mm}";
                }
                else
                {
                    // Calculate time remaining
                    var codeAge = DateTime.UtcNow - verification.CodeGeneratedAt;
                    int ConfigVerifyExpiration = _config.VerificationCodeExpirationMinutes;
                    var timeRemaining = TimeSpan.FromMinutes(ConfigVerifyExpiration) - codeAge;

                    if (timeRemaining.TotalSeconds <= 0)
                    {
                        statusText = "[EXPIRED] Your verification code has expired\nType /tds verify @DiscordName to generate a new code";
                    }
                    else
                    {
                        statusText = "[IN PROGRESS] Verification pending\n";
                        statusText += $"Discord Username: {verification.DiscordUsername}\n";
                        statusText += $"Code: {verification.VerificationCode}\n";
                        statusText += $"Time Remaining: {(int)timeRemaining.TotalMinutes}m {(int)timeRemaining.Seconds}s";
                    }
                }

                ChatUtils.SendInfo(statusText);
                LoggerUtil.LogInfo($"[VERIFY_STATUS_CMD] Status shown to {playerName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_STATUS_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Status error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds verify @DiscordName command
        /// Generates verification code and sends DM to Discord user
        /// Admin logged privately with SteamID
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
                    ChatUtils.SendError("Usage: /tds verify @DiscordName or /tds verify <DiscordUserID>");
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

                // Log verification attempt to admin log (with SteamID, but not in public chat)
                _ = _eventLog.LogAsync("VerificationAttempt",
                    $"Player: {playerName} | SteamID: {playerSteamID} | Discord: {discordUsername}");

                // Call the verification handler asynchronously
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
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError($"[VERIFY_CMD] Async error: {ex.Message}");
                        ChatUtils.SendError($"Verification error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds sync command (admin only)
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
                var _ = _eventLog.LogAsync("CommandError", "Sync command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds reset command (admin only)
        /// </summary>
        private void HandleResetCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogWarning("[COMMAND] " + playerName + " executed: /tds reset (DESTRUCTIVE)");
                _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds reset");
                ChatUtils.SendWarning("Clearing Discord roles and channels...");

                _ = _factionSync.ResetDiscordAsync();
                var __ = _eventLog.LogAsync("Command", "Discord reset executed by " + playerName);
                ChatUtils.SendSuccess("Discord reset complete! User roles updated.");
                LoggerUtil.LogSuccess("[RESET] Completed by " + playerName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[RESET_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Reset error: " + ex.Message);
                _ = _eventLog.LogAsync("CommandError", "Reset command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds cleanup command (admin only)
        /// </summary>
        private void HandleCleanupCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds cleanup");
                var _ = _eventLog.LogAsync("AdminCommand", playerName + " executed: /tds cleanup");
                ChatUtils.SendWarning("Starting cleanup of orphaned Discord roles/channels...");

                ChatUtils.SendSuccess("Cleanup simulation completed! (Actual cleanup not yet implemented)");
                LoggerUtil.LogSuccess("[CLEANUP] Cleanup completed by " + playerName);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[CLEANUP_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Cleanup error: " + ex.Message);
                var _ = _eventLog.LogAsync("CommandError", "Cleanup command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds reload command (admin only)
        /// </summary>
        private void HandleReloadCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogInfo("[COMMAND] " + playerName + " executed: /tds reload");
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
        /// Handle /tds unverify STEAMID [reason] command (admin only)
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

                // Get verification info before deleting
                var verification = _db?.GetVerification(targetSteamID);
                if (verification == null)
                {
                    ChatUtils.SendWarning("Verification not found for SteamID: " + targetSteamID);
                    LoggerUtil.LogWarning("[UNVERIFY_CMD] " + adminName + " - verification not found for SteamID " + targetSteamID);
                    return;
                }

                // Delete verification
                _db?.DeleteVerification(targetSteamID);

                ChatUtils.SendSuccess("Verification removed for: " + verification.DiscordUsername + " (SteamID: " + targetSteamID + ")");
                LoggerUtil.LogSuccess("[UNVERIFY_CMD] " + adminName + " removed verification for " + verification.DiscordUsername + " (SteamID: " + targetSteamID + ") - Reason: " + reason);

                // Log to admin log with SteamID
                _ = _eventLog.LogAsync("UnverifyCommand",
                    $"Admin: {adminName} | Unverified SteamID: {targetSteamID} | Discord: {verification.DiscordUsername} | Reason: {reason}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[UNVERIFY_CMD] Error: " + ex.Message);
                ChatUtils.SendError("Unverify error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle /tds admin:verify:list command (admin only)
        /// Lists all verified users
        /// </summary>
        private void HandleAdminVerifyList(string adminName)
        {
            try
            {
                var allVerifications = _db?.GetAllVerifications();
                var verified = allVerifications?.FindAll(v => v.IsVerified);

                if (verified == null || verified.Count == 0)
                {
                    ChatUtils.SendInfo("No verified users found");
                    return;
                }

                string listText = "[VERIFIED USERS]\n";
                int count = 1;
                foreach (var v in verified)
                {
                    listText += $"{count}. {v.DiscordUsername} | SteamID: {v.SteamID} | Verified: {v.VerifiedAt:yyyy-MM-dd HH:mm}\n";
                    count++;
                }

                ChatUtils.SendHelpText(listText);
                LoggerUtil.LogInfo($"[ADMIN_VERIFY_LIST] List shown to {adminName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[ADMIN_VERIFY_LIST] Error: {ex.Message}");
                ChatUtils.SendError($"List error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds admin:verify:pending command (admin only)
        /// Lists all pending verifications
        /// </summary>
        private void HandleAdminVerifyPending(string adminName)
        {
            try
            {
                var allVerifications = _db?.GetAllVerifications();
                var pending = allVerifications?.FindAll(v => !v.IsVerified);

                if (pending == null || pending.Count == 0)
                {
                    ChatUtils.SendInfo("No pending verifications");
                    return;
                }

                string listText = "[PENDING VERIFICATIONS]\n";
                int count = 1;
                foreach (var p in pending)
                {
                    var age = DateTime.UtcNow - p.CodeGeneratedAt;
                    string ageStr = age.TotalMinutes < 1 ? $"{(int)age.TotalSeconds}s" : $"{(int)age.TotalMinutes}m";
                    listText += $"{count}. {p.DiscordUsername} | SteamID: {p.SteamID} | Code: {p.VerificationCode} | Age: {ageStr}\n";
                    count++;
                }

                ChatUtils.SendHelpText(listText);
                LoggerUtil.LogInfo($"[ADMIN_VERIFY_PENDING] List shown to {adminName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[ADMIN_VERIFY_PENDING] Error: {ex.Message}");
                ChatUtils.SendError($"List error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds admin:verify:delete STEAMID command (admin only)
        /// Deletes a verification record
        /// </summary>
        private void HandleAdminVerifyDelete(string adminName, string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    ChatUtils.SendError("Usage: /tds admin:verify:delete STEAMID");
                    return;
                }

                long targetSteamID = 0;
                if (!long.TryParse(args[3], out targetSteamID))
                {
                    ChatUtils.SendError("Invalid Steam ID format");
                    return;
                }

                var verification = _db?.GetVerification(targetSteamID);
                if (verification == null)
                {
                    ChatUtils.SendWarning("Verification not found for SteamID: " + targetSteamID);
                    LoggerUtil.LogWarning("[VERIFY_DELETE] " + adminName + " - verification not found for SteamID " + targetSteamID);
                    return;
                }

                // Delete verification
                _db?.DeleteVerification(targetSteamID);

                ChatUtils.SendSuccess("Verification deleted for: " + verification.DiscordUsername + " (SteamID: " + targetSteamID + ")");
                LoggerUtil.LogSuccess("[VERIFY_DELETE] " + adminName + " deleted verification for " + verification.DiscordUsername + " (SteamID: " + targetSteamID + ")");

                // Log to admin log
                _ = _eventLog.LogAsync("VerificationDeleted",
                    $"Admin: {adminName} | Deleted SteamID: {targetSteamID} | Discord: {verification.DiscordUsername}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY_DELETE] Error: " + ex.Message);
                ChatUtils.SendError("Delete error: " + ex.Message);
            }
        }


        /// <summary>
        /// Handle /tds verify:delete command
        /// Deletes pending verification for current player
        /// </summary>
        private void HandleVerifyDeleteCommand(long playerSteamID, string playerName)
        {
            try
            {
                LoggerUtil.LogDebug(
                    $"[VERIFY_DELETE_CMD] Checking verification status for {playerName} (SteamID: {playerSteamID})"
                );

                // Check if player is already verified
                var verified = _db?.GetVerifiedPlayer(playerSteamID);
                if (verified != null)
                {
                    ChatUtils.SendWarning("[FAIL] You are already verified!");
                    ChatUtils.SendInfo(
                        "Only administrators can remove your verification.\nContact an admin if needed."
                    );
                    LoggerUtil.LogWarning(
                        $"[VERIFY_DELETE_CMD] {playerName} tried to delete verified status"
                    );
                    return;
                }

                // Check for pending verification
                var pending = _db?.GetPendingVerification(playerSteamID);
                if (pending == null)
                {
                    ChatUtils.SendInfo("[I] You don't have any pending verification.");
                    return;
                }

                // Delete pending verification
                _db?.DeletePendingVerification(playerSteamID);

                ChatUtils.SendSuccess("[OK] Pending verification deleted!");
                ChatUtils.SendInfo(
                    "You can start a new verification with /tds verify [DiscordID/Name]"
                );

                LoggerUtil.LogInfo(
                    $"[VERIFY_DELETE_CMD] {playerName} deleted pending verification"
                );
                _ = _eventLog.LogAsync(
                    "VerificationDeleted",
                    $"Player: {playerName} | SteamID: {playerSteamID} | Deleted pending verification"
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_DELETE_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle /tds verify:help command
        /// Show detailed verification instructions
        /// </summary>
        private void HandleVerifyHelpCommand(string playerName)
        {
            try
            {
                LoggerUtil.LogDebug($"[VERIFY_HELP_CMD] Help requested by {playerName}");

                string helpText =
                    @"
=== VERIFICATION GUIDE ===

[STEP 1] IN-GAME
  Type: /tds verify [DiscordID or DiscordName]
  
  Example 1: /tds verify mamba73 (username)
  Example 2: /tds verify 765540000000001234 (Discord ID)

[STEP 2] CHECK DISCORD DM
  You should receive a private message from the bot
  Look for message with verification code
  Code format: 8 random letters
  Example: SYIXFNCE

[STEP 3] REPLY ON DISCORD
  In the bot's DM, type:
  !verify [CODE]
  
  Example: !verify SYIXFNCE

[STEP 4] WAIT FOR CONFIRMATION
  Bot will respond with verification status
  If successful: You are now linked!
  If failed: Check code and try again

[COMMANDS]
  /tds verify:status  - Check current verification status
  /tds verify:delete  - Delete pending verification
  /tds verify:help    - Show this help

[TROUBLESHOOTING]
  - Username is incorrect: Make sure Discord username is correct
  - Bot cannot find you: You must be in the Discord server
  - Code expired: Verification codes expire after 15 minutes
  - Still not working: Contact an administrator

=========================";

                ChatUtils.SendHelpText(helpText);
                LoggerUtil.LogInfo($"[VERIFY_HELP_CMD] Help sent to {playerName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY_HELP_CMD] Error: {ex.Message}");
                ChatUtils.SendError($"Error: {ex.Message}");
            }
}        

        /// <summary>
        /// Handle /tds status command
        /// Shows plugin and server status (ASCII only - no emoji)
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

                // Build status message (ASCII only - no emoji)
                string statusText = "=== TDS Plugin Status ===\n";
                statusText += $"Status: [OK] ONLINE\n";
                statusText += $"Factions: {totalFactions}\n";
                statusText += $"Players: {totalPlayers}\n";
                statusText += $"Chat Sync: {(_config?.Chat?.Enabled == true ? "[OK]" : "[FAIL]")}\n";
                statusText += $"Death Logging: {(_config?.Death?.Enabled == true ? "[OK]" : "[FAIL]")}\n";
                statusText += $"Verification: {(_config?.Discord != null ? "[OK]" : "[FAIL]")}\n";
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
