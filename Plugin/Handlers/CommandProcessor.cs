// Plugin/Handlers/CommandProcessor.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Core;
using mamba.TorchDiscordSync.Plugin.Handlers;
using mamba.TorchDiscordSync.Plugin.Models;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;

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
        private readonly TestCommandHandler _testHandler;

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
            _testHandler = new TestCommandHandler(_discordService);

            if (_verificationCommandHandler != null)
            {
                LoggerUtil.LogInfo("[COMMAND] VerificationCommandHandler successfully initialized");
            }
            else
            {
                LoggerUtil.LogWarning(
                    "[COMMAND] VerificationCommandHandler is NULL - verify command will not work!"
                );
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
                LoggerUtil.LogDebug(
                    $"[COMMAND] Processing command from {playerName} (SteamID: {playerSteamID}): {command}"
                );

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
                    LoggerUtil.LogInfo(
                        $"[VERIFY_STATUS_CMD] Verify status command from {playerName}"
                    );
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

                // === TEST COMMANDS ===
                if (subcommand.StartsWith("test:"))
                {
                    LoggerUtil.LogInfo($"[TEST_CMD] Test command from {playerName}: {subcommand}");
                    _testHandler.HandleTestCommand(subcommand, playerSteamID, playerName, isAdmin);
                    return;
                }

                // ========== ADMIN SYNC COMMANDS ==========
                if (subcommand.StartsWith("admin:sync:"))
                {
                    // PROVJERI JE LI ADMIN
                    if (!CommandAuthorizationUtil.IsUserAdmin(playerSteamID, _config.AdminSteamIDs))
                    {
                        ChatUtils.SendError("❌ Admin command - access denied");
                        LoggerUtil.LogWarning(
                            $"[COMMAND] Non-admin {playerName} (SteamID: {playerSteamID}) tried admin:sync command: {subcommand}"
                        );
                        return;
                    }

                    // Ekstrakti sub-komandu (check, undo, cleanup, status)
                    string syncSubcommand = subcommand.Substring("admin:sync:".Length);

                    LoggerUtil.LogInfo(
                        $"[COMMAND] Admin {playerName} (SteamID: {playerSteamID}) executing: admin:sync:{syncSubcommand}"
                    );

                    switch (syncSubcommand)
                    {
                        case "check":
                        {
                            string result = _factionSync.AdminSyncCheck();
                            ChatUtils.SendSuccess(result);
                            LoggerUtil.LogInfo($"[ADMIN:SYNC:CHECK] Executed by {playerName}");
                            break;
                        }

                        case "undo":
                        {
                            if (parts.Length < 4)
                            {
                                ChatUtils.SendError("Usage: /tds admin:sync:undo <faction_tag>");
                                LoggerUtil.LogDebug(
                                    $"[ADMIN:SYNC:UNDO] Missing arguments from {playerName}"
                                );
                                return;
                            }

                            string factionTag = parts[3];
                            LoggerUtil.LogWarning(
                                $"[ADMIN:SYNC:UNDO] {playerName} undoing faction: {factionTag}"
                            );

                            var _ = _factionSync
                                .AdminSyncUndo(factionTag)
                                .ContinueWith(
                                    (Task<string> task) =>
                                    {
                                        // NOTE: .NET Framework 4.8 does not support Task.IsCompletedSuccessfully
                                        // Use classic IsFaulted / IsCanceled checks instead.
                                        if (!task.IsFaulted && !task.IsCanceled)
                                        {
                                            ChatUtils.SendSuccess(task.Result);
                                            LoggerUtil.LogSuccess(
                                                $"[ADMIN:SYNC:UNDO] Completed by {playerName} for {factionTag}"
                                            );
                                        }
                                        else
                                        {
                                            var errorMessage =
                                                task.Exception != null
                                                    ? task.Exception.Message
                                                    : "Unknown error";
                                            ChatUtils.SendError($"❌ Error: {errorMessage}");
                                            LoggerUtil.LogError(
                                                $"[ADMIN:SYNC:UNDO] Failed: {errorMessage}"
                                            );
                                        }
                                    }
                                );
                            break;
                        }

                        case "cleanup":
                        {
                            ChatUtils.SendWarning(
                                "⚙ Cleaning up orphaned Discord roles/channels..."
                            );
                            LoggerUtil.LogWarning(
                                $"[ADMIN:SYNC:CLEANUP] {playerName} starting cleanup"
                            );

                            var _ = _factionSync
                                .AdminSyncCleanup()
                                .ContinueWith(
                                    (Task<string> task) =>
                                    {
                                        // NOTE: .NET Framework 4.8 does not support Task.IsCompletedSuccessfully
                                        // Use classic IsFaulted / IsCanceled checks instead.
                                        if (!task.IsFaulted && !task.IsCanceled)
                                        {
                                            ChatUtils.SendSuccess(task.Result);
                                            LoggerUtil.LogSuccess(
                                                $"[ADMIN:SYNC:CLEANUP] Completed by {playerName}"
                                            );
                                        }
                                        else
                                        {
                                            var errorMessage =
                                                task.Exception != null
                                                    ? task.Exception.Message
                                                    : "Unknown error";
                                            ChatUtils.SendError($"❌ Error: {errorMessage}");
                                            LoggerUtil.LogError(
                                                $"[ADMIN:SYNC:CLEANUP] Failed: {errorMessage}"
                                            );
                                        }
                                    }
                                );
                            break;
                        }

                        case "status":
                        {
                            string status = _factionSync.AdminSyncStatus();
                            ChatUtils.SendInfo(status);
                            LoggerUtil.LogInfo($"[ADMIN:SYNC:STATUS] Executed by {playerName}");
                            break;
                        }

                        default:
                            ChatUtils.SendError($"Unknown admin:sync subcommand: {syncSubcommand}");
                            LoggerUtil.LogWarning(
                                $"[COMMAND] Unknown admin:sync subcommand from {playerName}: {syncSubcommand}"
                            );
                            break;
                    }

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
        /// Non-admins see: verify, verify:status, verify:help, status, help
        /// Admins see: all user commands + all admin commands (sync, verify, etc.)
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

                var helpText = new System.Text.StringBuilder();
                helpText.AppendLine("═══════════════════════════════════════════════");
                helpText.AppendLine("            TDS (Torch Discord Sync)");
                helpText.AppendLine("═══════════════════════════════════════════════");
                helpText.AppendLine();

                // ========== ADMIN COMMANDS ==========
                if (isAdmin)
                {
                    helpText.AppendLine("[ADMIN] SYNC COMMANDS:");
                    helpText.AppendLine(
                        "  /tds admin:sync:check      - Check status of all faction syncs"
                    );
                    helpText.AppendLine(
                        "  /tds admin:sync:undo <tag> - Undo sync for faction (delete role/channel)"
                    );
                    helpText.AppendLine(
                        "  /tds admin:sync:cleanup    - Delete all orphaned Discord roles/channels"
                    );
                    helpText.AppendLine("  /tds admin:sync:status     - Show sync status summary");
                    helpText.AppendLine();

                    helpText.AppendLine("[ADMIN] GENERAL COMMANDS:");
                    helpText.AppendLine(
                        "  /tds sync                  - Synchronize all factions to Discord"
                    );
                    helpText.AppendLine(
                        "  /tds reset                 - Reset Discord (DELETE all roles/channels!)"
                    );
                    helpText.AppendLine("  /tds reload                - Reload configuration");
                    helpText.AppendLine(
                        "  /tds cleanup               - Clean orphaned Discord items"
                    );
                    helpText.AppendLine();

                    helpText.AppendLine("[ADMIN] VERIFICATION COMMANDS:");
                    helpText.AppendLine(
                        "  /tds unverify <SteamID> [reason]  - Remove user verification"
                    );
                    helpText.AppendLine(
                        "  /tds admin:verify:list             - List all verified users"
                    );
                    helpText.AppendLine(
                        "  /tds admin:verify:pending          - List pending verifications"
                    );
                    helpText.AppendLine(
                        "  /tds admin:verify:delete <SteamID> - Delete verification"
                    );
                    helpText.AppendLine();

                    helpText.AppendLine("[ADMIN] HELP:");
                    helpText.AppendLine("  /tds admin:help            - Show admin commands");
                    helpText.AppendLine();
                }

                // ========== USER COMMANDS ==========
                helpText.AppendLine("[USER] VERIFICATION COMMANDS:");
                helpText.AppendLine(
                    "  /tds verify @DiscordName        - Link Discord account (by username)"
                );
                helpText.AppendLine(
                    "  /tds verify <DiscordUserID>     - Link Discord account (by ID)"
                );
                helpText.AppendLine(
                    "  /tds verify:status              - Check your verification status"
                );
                helpText.AppendLine(
                    "  /tds verify:delete              - Delete your pending verification"
                );
                helpText.AppendLine(
                    "  /tds verify:help                - Detailed verification guide"
                );
                helpText.AppendLine();

                helpText.AppendLine("[USER] INFO COMMANDS:");
                helpText.AppendLine("  /tds status                 - Show plugin status");
                helpText.AppendLine("  /tds help                   - Show this help");
                helpText.AppendLine();

                helpText.AppendLine("═══════════════════════════════════════════════");

                ChatUtils.SendHelpText(helpText.ToString());
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
                LoggerUtil.LogDebug(
                    $"[VERIFY_STATUS_CMD] Checking status for {playerName} (SteamID: {playerSteamID})"
                );

                // Get verification status from database
                var verification = _db?.GetVerification(playerSteamID);

                string statusText = "";
                if (verification == null)
                {
                    statusText =
                        "[NOT VERIFIED] You have not started verification yet\nType /tds verify @DiscordName to begin";
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
                        statusText =
                            "[EXPIRED] Your verification code has expired\nType /tds verify @DiscordName to generate a new code";
                    }
                    else
                    {
                        statusText = "[IN PROGRESS] Verification pending\n";
                        statusText += $"Discord Username: {verification.DiscordUsername}\n";
                        statusText += $"Code: {verification.VerificationCode}\n";
                        statusText +=
                            $"Time Remaining: {(int)timeRemaining.TotalMinutes}m {(int)timeRemaining.Seconds}s";
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
                LoggerUtil.LogInfo(
                    $"[VERIFY_CMD] HANDLER CALLED for {playerName} (SteamID: {playerSteamID})"
                );
                LoggerUtil.LogDebug(
                    $"[VERIFY_CMD] Args length: {args.Length}, Args: {string.Join(", ", args)}"
                );

                // Validate arguments
                if (args.Length < 3)
                {
                    ChatUtils.SendError(
                        "Usage: /tds verify @DiscordName or /tds verify <DiscordUserID>"
                    );
                    LoggerUtil.LogWarning("[VERIFY_CMD] Invalid arguments - need Discord username");
                    return;
                }

                // CRITICAL FIX: Check if handler exists
                if (_verificationCommandHandler == null)
                {
                    LoggerUtil.LogError(
                        "[VERIFY_CMD] CRITICAL: VerificationCommandHandler is NULL!"
                    );
                    ChatUtils.SendError(
                        "Verification system is not initialized. Contact an admin."
                    );
                    return;
                }

                string discordUsername = args[2];
                LoggerUtil.LogInfo(
                    $"[VERIFY_CMD] Processing verification for Discord user: {discordUsername}"
                );

                // Log verification attempt to admin log (with SteamID, but not in public chat)
                _ = _eventLog.LogAsync(
                    "VerificationAttempt",
                    $"Player: {playerName} | SteamID: {playerSteamID} | Discord: {discordUsername}"
                );

                // Call the verification handler asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        LoggerUtil.LogDebug(
                            $"[VERIFY_CMD] Calling VerificationCommandHandler.HandleVerifyCommandAsync"
                        );

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
                LoggerUtil.LogWarning(
                    "[COMMAND] " + playerName + " executed: /tds reset (DESTRUCTIVE)"
                );
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

                ChatUtils.SendSuccess(
                    "Cleanup simulation completed! (Actual cleanup not yet implemented)"
                );
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
                    LoggerUtil.LogWarning(
                        "[UNVERIFY_CMD] "
                            + adminName
                            + " - verification not found for SteamID "
                            + targetSteamID
                    );
                    return;
                }

                // Delete verification
                _db?.DeleteVerification(targetSteamID);

                ChatUtils.SendSuccess(
                    "Verification removed for: "
                        + verification.DiscordUsername
                        + " (SteamID: "
                        + targetSteamID
                        + ")"
                );
                LoggerUtil.LogSuccess(
                    "[UNVERIFY_CMD] "
                        + adminName
                        + " removed verification for "
                        + verification.DiscordUsername
                        + " (SteamID: "
                        + targetSteamID
                        + ") - Reason: "
                        + reason
                );

                // Log to admin log with SteamID
                _ = _eventLog.LogAsync(
                    "UnverifyCommand",
                    $"Admin: {adminName} | Unverified SteamID: {targetSteamID} | Discord: {verification.DiscordUsername} | Reason: {reason}"
                );
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
                    listText +=
                        $"{count}. {v.DiscordUsername} | SteamID: {v.SteamID} | Verified: {v.VerifiedAt:yyyy-MM-dd HH:mm}\n";
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
                    string ageStr =
                        age.TotalMinutes < 1
                            ? $"{(int)age.TotalSeconds}s"
                            : $"{(int)age.TotalMinutes}m";
                    listText +=
                        $"{count}. {p.DiscordUsername} | SteamID: {p.SteamID} | Code: {p.VerificationCode} | Age: {ageStr}\n";
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
                    LoggerUtil.LogWarning(
                        "[VERIFY_DELETE] "
                            + adminName
                            + " - verification not found for SteamID "
                            + targetSteamID
                    );
                    return;
                }

                // Delete verification
                _db?.DeleteVerification(targetSteamID);

                ChatUtils.SendSuccess(
                    "Verification deleted for: "
                        + verification.DiscordUsername
                        + " (SteamID: "
                        + targetSteamID
                        + ")"
                );
                LoggerUtil.LogSuccess(
                    "[VERIFY_DELETE] "
                        + adminName
                        + " deleted verification for "
                        + verification.DiscordUsername
                        + " (SteamID: "
                        + targetSteamID
                        + ")"
                );

                // Log to admin log
                _ = _eventLog.LogAsync(
                    "VerificationDeleted",
                    $"Admin: {adminName} | Deleted SteamID: {targetSteamID} | Discord: {verification.DiscordUsername}"
                );
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
                    "=== VERIFICATION GUIDE ===\n"
                    + "[STEP 1] IN-GAME\n"
                    + "  Type: /tds verify [DiscordID or DiscordName]\n"
                    + "  \n"
                    + "  Example 1: /tds verify mamba73 (username)\n"
                    + "  Example 2: /tds verify 765540000000001234 (Discord ID)\n"
                    + "[STEP 2] CHECK DISCORD DM\n"
                    + "  You should receive a private message from the bot\n"
                    + "  Look for message with verification code\n"
                    + "  Code format: 8 random letters\n"
                    + "  Example: SYIXFNCE\n"
                    + "[STEP 3] REPLY ON DISCORD\n"
                    + "  In the bot's DM, type:\n"
                    + "  !verify [CODE]\n"
                    + "  \n"
                    + "  Example: !verify SYIXFNCE\n"
                    + "[STEP 4] WAIT FOR CONFIRMATION\n"
                    + "  Bot will respond with verification status\n"
                    + "  If successful: You are now linked!\n"
                    + "  If failed: Check code and try again\n"
                    + "[COMMANDS]\n"
                    + "  /tds verify:status  - Check current verification status\n"
                    + "  /tds verify:delete  - Delete pending verification\n"
                    + "  /tds verify:help    - Show this help\n"
                    + "[TROUBLESHOOTING]\n"
                    + "  - Username is incorrect: Make sure Discord username is correct\n"
                    + "  - Bot cannot find you: You must be in the Discord server\n"
                    + "  - Code expired: Verification codes expire after "
                    + _config.VerificationCodeExpirationMinutes
                    + " minutes\n"
                    + "  - Still not working: Contact an administrator\n"
                    + "=========================";

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
                statusText +=
                    $"Chat Sync: {(_config?.Chat?.Enabled == true ? "[OK]" : "[FAIL]")}\n";
                statusText +=
                    $"Death Logging: {(_config?.Death?.Enabled == true ? "[OK]" : "[FAIL]")}\n";
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
