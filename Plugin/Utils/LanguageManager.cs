// Plugin/Utils/LanguageManager.cs
// MAMBA LANGUAGE MANAGER
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace mamba.TorchDiscordSync.Plugin.Utils
{
    /// <summary>
    /// Static localization provider.
    /// Call Lang.Load() once after config is read, then use Lang.Get("Key.Name")
    /// anywhere in the codebase.  Missing keys silently fall back to built-in
    /// English defaults so old translation files never break the plugin.
    /// </summary>
    public static class Lang
    {
        // ---- internal state ----
        private static Dictionary<string, string> _active;
        private static readonly Dictionary<string, string> _defaults;

        // ---- INI metadata ----
        private const int    INI_VERSION    = 1;
        private const string DEFAULT_LOCALE = "en-US";
        private const string FOLDER_NAME = "Localization";

        // ============================================================
        // STATIC CONSTRUCTOR – build English defaults
        // ============================================================
        static Lang()
        {
            _defaults = new Dictionary<string, string>(StringComparer.Ordinal);
            BuildDefaults();
            _active = new Dictionary<string, string>(_defaults, StringComparer.Ordinal);
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Load a language profile from [configDir]/[languageCode].ini.
        /// Falls back to built-in English for any missing key.
        /// Auto-generates en-US.ini template when languageCode is "en-US".
        /// </summary>
        public static void Load(string languageCode, string configDir)
        {
            // Reset to defaults first
            _active = new Dictionary<string, string>(_defaults, StringComparer.Ordinal);

            if (string.IsNullOrEmpty(languageCode))
                languageCode = DEFAULT_LOCALE;

            // Target the 'Localization' subdirectory inside the config folder
            string localizationDir = Path.Combine(configDir, FOLDER_NAME);

            try
            {
                if (!Directory.Exists(localizationDir))
                {
                    Directory.CreateDirectory(localizationDir);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[Translation] Failed to create Localization folder: " + ex.Message);
                return;
            }

            string iniPath = Path.Combine(localizationDir, languageCode + ".ini");

            // Auto-generate en-US template if missing
            if (languageCode == DEFAULT_LOCALE && !File.Exists(iniPath))
            {
                try
                {
                    GenerateTemplate(iniPath);
                    LoggerUtil.LogInfo("[Translation] Generated en-US.ini template: " + iniPath);
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("[Translation] Could not generate en-US.ini: " + ex.Message);
                }
                return; // defaults already loaded
            }

            if (!File.Exists(iniPath))
            {
                LoggerUtil.LogInfo(
                    string.Format(
                        "[Translation] {0}.ini not found – using built-in English defaults",
                        languageCode
                    )
                );
                return;
            }

            // Parse the INI file
            int fileVersion = 0;
            int keysOverridden = 0;

            try
            {
                using (StreamReader reader = new StreamReader(iniPath, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();

                        // Skip blank lines, comments, and section headers
                        if (line.Length == 0)                  continue;
                        if (line.StartsWith(";"))              continue;
                        if (line.StartsWith("#"))              continue;
                        if (line.StartsWith("[") &&
                            line.EndsWith("]"))                continue;

                        int eq = line.IndexOf('=');
                        if (eq < 1) continue;

                        string key   = line.Substring(0, eq).Trim();
                        string value = line.Substring(eq + 1).Trim();

                        // Unescape \n sequences
                        value = value.Replace("\\n", "\n");

                        // Capture metadata version without overriding real keys
                        if (key == "ConfigVersion")
                        {
                            int.TryParse(value, out fileVersion);
                            continue;
                        }
                        if (key == "LanguageCode" || key == "LanguageName")
                            continue;

                        if (_defaults.ContainsKey(key))
                        {
                            _active[key] = value;
                            keysOverridden++;
                        }
                    }
                }

                // Version mismatch warning
                if (fileVersion > 0 && fileVersion != INI_VERSION)
                {
                    LoggerUtil.LogWarning(
                        string.Format(
                            "[Translation] Warn: {0}.ini is outdated (Version {1}, expected {2}). Missing strings will use English fallback.",
                            languageCode,
                            fileVersion,
                            INI_VERSION
                        )
                    );
                }

                LoggerUtil.LogInfo(
                    string.Format(
                        "[Translation] Loaded language profile: {0} ({1} keys overridden)",
                        languageCode,
                        keysOverridden
                    )
                );
            }
            catch (Exception ex)
            {
                _active = new Dictionary<string, string>(_defaults, StringComparer.Ordinal);
                LoggerUtil.LogError(
                    string.Format(
                        "[Translation] Error loading {0}.ini. Reverting to internal English defaults. {1}",
                        languageCode,
                        ex.Message
                    )
                );
            }
        }

        /// <summary>
        /// Get a localized string by key.
        /// Falls back to the built-in English default, then to the raw key if
        /// the key is somehow absent from both dictionaries.
        /// </summary>
        public static string Get(string key)
        {
            string value;
            if (_active.TryGetValue(key, out value))
                return value;
            if (_defaults.TryGetValue(key, out value))
                return value;
            return key; // last-resort: return key itself
        }

        // ============================================================
        // TEMPLATE GENERATOR
        // ============================================================

        private static void GenerateTemplate(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine(";  [X] DO NOT TRANSLATE THIS SECTION");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("[Metadata]");
            sb.AppendLine("ConfigVersion = " + INI_VERSION);
            sb.AppendLine("LanguageCode = en-US");
            sb.AppendLine("LanguageName = English");
            sb.AppendLine();
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine(";  [V] TRANSLATE EVERYTHING BELOW THIS LINE");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine();

            string currentSection = "";
            foreach (var kv in _defaults)
            {
                // Derive section from key prefix (e.g. "Help.Title" -> "[Help]")
                int dot = kv.Key.IndexOf('.');
                string section = dot > 0 ? kv.Key.Substring(0, dot) : "General";

                if (section != currentSection)
                {
                    sb.AppendLine();
                    sb.AppendLine("[" + section + "]");
                    currentSection = section;
                }

                // Escape newlines for storage
                string escaped = kv.Value.Replace("\n", "\\n");
                sb.AppendLine(kv.Key + " = " + escaped);
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ============================================================
        // DEFAULT ENGLISH STRINGS
        // ============================================================

        private static void BuildDefaults()
        {
            // ---- Help ----
            _defaults["Help.Separator"]             = "===============================================";
            _defaults["Help.Title"]                 = "         TDS (Torch Discord Sync)";
            _defaults["Help.Admin.SyncHeader"]       = "[ADMIN] SYNC COMMANDS:";
            _defaults["Help.Admin.SyncCheck"]        = "  /tds admin:sync:check      - Check status of all faction syncs";
            _defaults["Help.Admin.SyncUndo"]         = "  /tds admin:sync:undo <tag> - Undo sync for faction";
            _defaults["Help.Admin.SyncCleanup"]      = "  /tds admin:sync:cleanup    - Delete orphaned Discord roles/channels";
            _defaults["Help.Admin.SyncStatus"]       = "  /tds admin:sync:status     - Show sync status summary";
            _defaults["Help.Admin.GeneralHeader"]    = "[ADMIN] GENERAL COMMANDS:";
            _defaults["Help.Admin.Sync"]             = "  /tds sync      - Synchronize all factions to Discord";
            _defaults["Help.Admin.Reset"]            = "  /tds reset     - Reset Discord (DELETE all roles/channels!)";
            _defaults["Help.Admin.Reload"]           = "  /tds reload    - Reload configuration";
            _defaults["Help.Admin.Cleanup"]          = "  /tds cleanup   - Clean orphaned Discord items";
            _defaults["Help.Admin.VerifyHeader"]     = "[ADMIN] VERIFICATION COMMANDS:";
            _defaults["Help.Admin.Unverify"]         = "  /tds unverify <SteamID> [reason]   - Remove user verification";
            _defaults["Help.Admin.VerifyList"]       = "  /tds admin:verify:list              - List all verified users";
            _defaults["Help.Admin.VerifyPending"]    = "  /tds admin:verify:pending           - List pending verifications";
            _defaults["Help.Admin.VerifyDelete"]     = "  /tds admin:verify:delete <SteamID> - Delete verification record";
            _defaults["Help.User.VerifyHeader"]      = "[USER] VERIFICATION COMMANDS:";
            _defaults["Help.User.VerifyByName"]      = "  /tds verify @DiscordName    - Link Discord account (by username)";
            _defaults["Help.User.VerifyById"]        = "  /tds verify <DiscordUserID> - Link Discord account (by ID)";
            _defaults["Help.User.VerifyStatus"]      = "  /tds verify:status          - Check your verification status";
            _defaults["Help.User.VerifyDelete"]      = "  /tds verify:delete          - Delete pending verification";
            _defaults["Help.User.VerifyHelp"]        = "  /tds verify:help            - Detailed verification guide";
            _defaults["Help.User.InfoHeader"]        = "[USER] INFO COMMANDS:";
            _defaults["Help.User.Status"]            = "  /tds status - Show plugin status";
            _defaults["Help.User.Help"]              = "  /tds help   - Show this help";

            // ---- Status command ----
            _defaults["Status.Header"]   = "=== TDS Plugin Status ===";
            _defaults["Status.Online"]   = "Status: [OK] ONLINE";
            _defaults["Status.Factions"] = "Factions: {0}";
            _defaults["Status.Players"]  = "Players:  {0}";
            _defaults["Status.Chat"]     = "Chat Sync:    {0}";
            _defaults["Status.Death"]    = "Death Logging:{0}";
            _defaults["Status.Verify"]   = "Verification: {0}";
            _defaults["Status.Footer"]   = "=======================";
            _defaults["Status.OK"]       = "[OK]";
            _defaults["Status.Fail"]     = "[FAIL]";

            // ---- Verify status ----
            _defaults["VerifyStatus.Verified"]    = "[VERIFIED] You are verified!\nDiscord: {0}\nVerified at: {1}";
            _defaults["VerifyStatus.Expired"]     = "[EXPIRED] Your verification code has expired\nType /tds verify @DiscordName to generate a new code";
            _defaults["VerifyStatus.Pending"]     = "[IN PROGRESS] Verification pending\nDiscord Username: {0}\nCode: {1}\nTime Remaining: {2}m {3}s";
            _defaults["VerifyStatus.NotVerified"] = "[NOT VERIFIED] You have not started verification yet\nType /tds verify @DiscordName to begin";

            // ---- Verify:help ----
            _defaults["VerifyHelp.Header"]       = "=== VERIFICATION GUIDE ===";
            _defaults["VerifyHelp.Step1Header"]  = "[STEP 1] IN-GAME";
            _defaults["VerifyHelp.Step1Line1"]   = "  Type: /tds verify [DiscordID or DiscordName]";
            _defaults["VerifyHelp.Step1Line2"]   = "  Example 1: /tds verify mamba73 (username)";
            _defaults["VerifyHelp.Step1Line3"]   = "  Example 2: /tds verify 765540000000001234 (Discord ID)";
            _defaults["VerifyHelp.Step2Header"]  = "[STEP 2] CHECK DISCORD DM";
            _defaults["VerifyHelp.Step2Line1"]   = "  You should receive a private message from the bot.";
            _defaults["VerifyHelp.Step2Line2"]   = "  Code format: 8 random letters, e.g. SYIXFNCE";
            _defaults["VerifyHelp.Step3Header"]  = "[STEP 3] REPLY ON DISCORD";
            _defaults["VerifyHelp.Step3Line1"]   = "  In the bot's DM type:  !verify SYIXFNCE";
            _defaults["VerifyHelp.Step4Header"]  = "[STEP 4] WAIT FOR CONFIRMATION";
            _defaults["VerifyHelp.Step4Line1"]   = "  Bot will respond with verification status.";
            _defaults["VerifyHelp.CmdHeader"]    = "[COMMANDS]";
            _defaults["VerifyHelp.Cmd1"]         = "  /tds verify:status  - Check current verification status";
            _defaults["VerifyHelp.Cmd2"]         = "  /tds verify:delete  - Delete pending verification";
            _defaults["VerifyHelp.Cmd3"]         = "  /tds verify:help    - Show this help";
            _defaults["VerifyHelp.TroubleHeader"] = "[TROUBLESHOOTING]";
            _defaults["VerifyHelp.Trouble1"]     = "  - Wrong username: Make sure your Discord username is correct.";
            _defaults["VerifyHelp.Trouble2"]     = "  - Bot cannot find you: You must be in the Discord server.";
            _defaults["VerifyHelp.Trouble3"]     = "  - Code expired: Codes expire after {0} minutes.";
            _defaults["VerifyHelp.Trouble4"]     = "  - Still not working: Contact an administrator.";
            _defaults["VerifyHelp.Footer"]       = "=========================";

            // ---- Verify command responses ----
            _defaults["VerifyCmd.UsageError"]    = "Error: Discord username is required. Usage: /tds verify @DiscordUsername";
            _defaults["VerifyCmd.InvalidLength"] = "Error: Invalid Discord username length (2-32 characters)";
            _defaults["VerifyCmd.PendingFailed"] = "Error: Could not create pending verification. Please try again.";
            _defaults["VerifyCmd.CodeNotFound"]  = "Error: Verification code not found. Please try again.";
            _defaults["VerifyCmd.DmFailed"]      = "Error: Could not find Discord user '{0}' or send DM.\nMake sure:\n  - Username is correct\n  - User is in the Discord server\n  - Bot has DM permissions";
            _defaults["VerifyCmd.CodeSent"]      = "Verification code sent to {0} via DM!\nCheck your Discord private messages\nCode expires in {1} minutes";
            _defaults["VerifyCmd.NotInitialized"]= "Verification system is not initialized. Contact an admin.";
            _defaults["VerifyCmd.Usage"]         = "Usage: /tds verify @DiscordName  OR  /tds verify <DiscordUserID>";

            // ---- Verify:delete ----
            _defaults["VerifyDelete.AlreadyVerified"] = "[!] You are already verified!";
            _defaults["VerifyDelete.ContactAdmin"]     = "Only administrators can remove your verification. Contact an admin.";
            _defaults["VerifyDelete.NoPending"]        = "[I] You do not have any pending verification.";
            _defaults["VerifyDelete.Deleted"]          = "[OK] Pending verification deleted!";
            _defaults["VerifyDelete.NewVerify"]        = "You can start a new verification with /tds verify [DiscordID/Name]";

            // ---- VerifyFromDiscord ----
            _defaults["VerifyDiscord.Unavailable"] = "Verification service unavailable";
            _defaults["VerifyDiscord.InvalidCode"] = "Invalid verification code";

            // ---- Unverify ----
            _defaults["Unverify.Usage"]      = "Usage: /tds unverify STEAMID [reason]";
            _defaults["Unverify.InvalidId"]  = "Invalid Steam ID format";
            _defaults["Unverify.NotFound"]   = "Verification not found for SteamID: {0}";
            _defaults["Unverify.Removed"]    = "Verification removed for: {0} (SteamID: {1})";
            _defaults["Unverify.NotFoundSimple"] = "Error: Verification not found for this Steam ID";
            _defaults["Unverify.Done"]       = "Verification removed";

            // ---- Admin verify commands ----
            _defaults["AdminVerify.NoVerified"]    = "No verified users found";
            _defaults["AdminVerify.ListHeader"]    = "[VERIFIED USERS]";
            _defaults["AdminVerify.NoPending"]     = "No pending verifications";
            _defaults["AdminVerify.PendingHeader"] = "[PENDING VERIFICATIONS]";
            _defaults["AdminVerify.UsageDelete"]   = "Usage: /tds admin:verify:delete STEAMID";
            _defaults["AdminVerify.NotFound"]      = "Verification not found for SteamID: {0}";
            _defaults["AdminVerify.Deleted"]       = "Verification deleted for: {0} (SteamID: {1})";
            _defaults["AdminVerify.InvalidId"]     = "Invalid Steam ID format";

            // ---- Admin sync ----
            _defaults["AdminSync.UndoUsage"]   = "Usage: /tds admin:sync:undo <faction_tag>";
            _defaults["AdminSync.CleanupStart"]= "[*] Cleaning up orphaned Discord roles/channels...";
            _defaults["AdminSync.UndoAllStart"]= "Running undo for ALL factions (Discord roles/channels + XML records)...";
            _defaults["AdminSync.UnknownSub"]  = "Unknown admin:sync subcommand: {0}";

            // ---- Admin general commands ----
            _defaults["Cmd.NotAuthorized"]      = "Command '{0}' requires admin privileges";
            _defaults["Cmd.Unknown"]             = "Unknown command: /tds {0}. Type /tds help for available commands.";
            _defaults["Cmd.Sync.Starting"]       = "Starting faction synchronization...";
            _defaults["Cmd.Sync.Complete"]       = "Synchronization complete!";
            _defaults["Cmd.Reset.Clearing"]      = "Clearing Discord roles and channels...";
            _defaults["Cmd.Reset.Complete"]      = "Discord reset complete!";
            _defaults["Cmd.Reload.Reloading"]    = "Reloading configuration and database...";
            _defaults["Cmd.Reload.Success"]      = "Configuration reloaded successfully";
            _defaults["Cmd.Reload.Failed"]       = "Failed to reload configuration - keeping old config";
            _defaults["Cmd.Reload.Complete"]     = "Reload complete!";
            _defaults["Cmd.Cleanup.Starting"]    = "Starting cleanup of orphaned Discord roles/channels...";
            _defaults["Cmd.Cleanup.Complete"]    = "Cleanup simulation completed!";
            _defaults["Cmd.AdminDenied"]         = "[!] Admin command - access denied";

            // ---- In-game verification notification ----
            _defaults["InGame.VerifyOK"]   = "[OK] Verification successful! Discord account linked.";
            _defaults["InGame.VerifyFail"] = "[FAIL] Verification failed: {0}";
        }
    }
}
