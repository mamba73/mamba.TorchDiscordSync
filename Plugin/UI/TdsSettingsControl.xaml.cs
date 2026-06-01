// Plugin/UI/TdsSettingsControl.xaml.cs
// MAMBA SETTINGS UI CODE-BEHIND

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Plugin.Config;
using mamba.TorchDiscordSync.Plugin.Utils;

namespace mamba.TorchDiscordSync.Plugin.UI
{
    /// <summary>
    /// Code-behind for the Torch Admin GUI settings panel.
    /// All data flows through MainConfig: LoadConfig() reads config into UI,
    /// SaveConfig() writes UI back to config and persists to disk.
    /// </summary>
    public partial class TdsSettingsControl : UserControl
    {
        private readonly mamba.TorchDiscordSync.MambaTorchDiscordSyncPlugin _plugin;
        private MainConfig _config;

        public TdsSettingsControl(mamba.TorchDiscordSync.MambaTorchDiscordSyncPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            _config = plugin?.Config;
            Loaded += OnControlLoaded;
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            PopulateLanguages();
            LoadConfig();
        }

        /// <summary>
        /// Scans the Localization directory for .ini files and populates the ComboBox dropdown items.
        /// </summary>
        private void PopulateLanguages()
        {
            try
            {
                CmbLanguage.Items.Clear();

                string configDir = MainConfig.GetConfigDirectory();
                string localizationDir = Path.Combine(configDir, "Localization");

                if (Directory.Exists(localizationDir))
                {
                    // fix: changed from "*.xml" to "*.ini" so UI recognizes translation files
                    string[] files = Directory.GetFiles(localizationDir, "*.ini");
                    foreach (string file in files)
                    {
                        string langCode = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(langCode))
                        {
                            CmbLanguage.Items.Add(langCode);
                        }
                    }
                }

                if (CmbLanguage.Items.Count == 0)
                {
                    CmbLanguage.Items.Add("en-US");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[SETTINGS_UI] PopulateLanguages error: " + ex.Message);
            }
        }

        // ============================================================
        // LOAD: config -> UI
        // ============================================================

        private void LoadConfig()
        {
            if (_config == null) return;

            try
            {
                // ---- General ----
                ChkEnabled.IsChecked = _config.Enabled;
                ChkDebug.IsChecked = _config.Debug;
                TxtAdminSteamIDs.Text = _config.AdminSteamIDs != null
                    ? string.Join(", ", _config.AdminSteamIDs)
                    : string.Empty;
                ChkShowNpcActivity.IsChecked = _config.ShowNpcActivity;
                TxtCleanupInterval.Text = _config.CleanupIntervalSeconds.ToString();
                TxtDamageHistoryMax.Text = _config.DamageHistoryMaxSeconds.ToString();

                string targetLang = _config.Language ?? "en-US";
                if (!CmbLanguage.Items.Contains(targetLang))
                {
                    CmbLanguage.Items.Add(targetLang);
                }
                CmbLanguage.SelectedItem = targetLang;

                // ---- Discord ----
                if (_config.Discord != null)
                {
                    TxtBotToken.Text = _config.Discord.BotToken ?? string.Empty;
                    TxtGuildId.Text = _config.Discord.GuildID.ToString();
                    TxtBotPrefix.Text = _config.Discord.BotPrefix ?? "!";
                    ChkEnableDm.IsChecked = _config.Discord.EnableDMNotifications;
                    TxtSyncInterval.Text = _config.Discord.SyncIntervalSeconds.ToString();
                    TxtVerifyExpiry.Text = _config.Discord.VerificationCodeExpirationMinutes.ToString();
                    TxtChatChannelId.Text = _config.Discord.ChatChannelId.ToString();
                    TxtStatusChannelId.Text = _config.Discord.StatusChannelId.ToString();
                    TxtSimSpeedChannelId.Text = _config.Discord.SimSpeedChannelId.ToString();
                    TxtPlayerCountChannelId.Text = _config.Discord.PlayerCountChannelId.ToString();
                    TxtStaffLogId.Text = _config.Discord.StaffLog.ToString();
                    TxtAdminAlertChannelId.Text = _config.Discord.AdminAlertChannelId.ToString();
                    TxtAdminBotChannelId.Text = _config.Discord.AdminBotChannelId.ToString();
                    TxtFactionCategoryId.Text = _config.Discord.FactionCategoryId.ToString();
                    TxtVerifiedRoleId.Text = _config.Discord.VerifiedRoleId.ToString();
                }

                // ---- Chat ----
                if (_config.Chat != null)
                {
                    ChkChatEnabled.IsChecked = _config.Chat.Enabled;
                    ChkServerToDiscord.IsChecked = _config.Chat.ServerToDiscord;
                    ChkBotToGame.IsChecked = _config.Chat.BotToGame;
                    ChkUseFactionChat.IsChecked = _config.Chat.UseFactionChat;
                    ChkStripEmojis.IsChecked = _config.Chat.StripEmojisForInGameChat;
                    TxtGameToDiscord.Text = _config.Chat.GameToDiscordFormat ?? string.Empty;
                    TxtDiscordToGame.Text = _config.Chat.DiscordToGameFormat ?? string.Empty;
                    TxtConnectMsg.Text = _config.Chat.ConnectMessage ?? string.Empty;
                    TxtJoinMsg.Text = _config.Chat.JoinMessage ?? string.Empty;
                    TxtLeaveMsg.Text = _config.Chat.LeaveMessage ?? string.Empty;
                    TxtFactionChatFormat.Text = _config.Chat.FactionChatFormat ?? string.Empty;
                    TxtFactionDiscordFormat.Text = _config.Chat.FactionDiscordFormat ?? string.Empty;
                    TxtGlobalColor.Text = _config.Chat.GlobalColor ?? "White";
                    TxtFactionColor.Text = _config.Chat.FactionColor ?? "Green";
                    ChkEnableModeration.IsChecked = _config.Chat.EnableModeration;
                    TxtMaxWarnings.Text = _config.Chat.MaxWarningsBeforeMute.ToString();
                    TxtMuteDuration.Text = _config.Chat.MuteDurationMinutes.ToString();
                    TxtMaxMutes.Text = _config.Chat.MaxMutesBeforeKick.ToString();
                    TxtChatAdminLogId.Text = _config.Chat.AdminLogChannelId.ToString();
                }

                // ---- Deaths ----
                if (_config.Death != null)
                {
                    ChkDeathEnabled.IsChecked = _config.Death.Enabled;
                    ChkDeathLogToDiscord.IsChecked = _config.Death.LogToDiscord;
                    ChkDeathAnnounceIngame.IsChecked = _config.Death.AnnounceInGame;
                    ChkDetectRetaliation.IsChecked = _config.Death.DetectRetaliation;
                    ChkEnableLocationZones.IsChecked = _config.Death.EnableLocationZones;
                    ChkGridDetection.IsChecked = _config.Death.GridDetectionEnabled;
                    ChkShowGridName.IsChecked = _config.Death.ShowGridName;
                    TxtRetaliationWindow.Text = _config.Death.RetaliationWindowMinutes.ToString();
                    TxtOldRevengeWindow.Text = _config.Death.OldRevengeWindowHours.ToString();
                    TxtDedupWindow.Text = _config.Death.MessageDeduplicationWindowSeconds.ToString();
                    TxtInnerSystemKm.Text = _config.Death.InnerSystemMaxKm.ToString();
                    TxtOuterSpaceKm.Text = _config.Death.OuterSpaceMaxKm.ToString();
                    TxtPlanetProxMult.Text = _config.Death.PlanetProximityMultiplier.ToString();
                    TxtDeathEmotes.Text = _config.Death.DeathMessageEmotes ?? string.Empty;
                }

                // ---- Monitoring ----
                if (_config.Monitoring != null)
                {
                    ChkMonEnabled.IsChecked = _config.Monitoring.Enabled;
                    ChkSimSpeedMon.IsChecked = _config.Monitoring.EnableSimSpeedMonitoring;
                    ChkSimSpeedAlerts.IsChecked = _config.Monitoring.EnableSimSpeedAlerts;
                    TxtSimSpeedThreshold.Text = _config.Monitoring.SimSpeedThreshold.ToString();
                    TxtSimSpeedCooldown.Text = _config.Monitoring.SimSpeedAlertCooldownSeconds.ToString();
                    TxtSimSpeedChannelFormat.Text = _config.Monitoring.SimSpeedChannelNameFormat ?? string.Empty;
                    TxtSimSpeedAlertMsg.Text = _config.Monitoring.SimSpeedAlertMessage ?? string.Empty;
                    ChkPlayerCountMon.IsChecked = _config.Monitoring.EnablePlayerCountMonitoring;
                    ChkPlayerCountAlerts.IsChecked = _config.Monitoring.EnablePlayerCountAlerts;
                    TxtStatusInterval.Text = _config.Monitoring.StatusUpdateIntervalSeconds.ToString();
                    TxtPlayerCountChannelFormat.Text = _config.Monitoring.PlayerCountChannelNameFormat ?? string.Empty;
                    TxtPlayerCountThreshold.Text = _config.Monitoring.PlayerCountAlertThreshold.ToString();
                    ChkAdminAlerts.IsChecked = _config.Monitoring.EnableAdminAlerts;
                    TxtSrvStartMsg.Text = _config.Monitoring.ServerStartedMessage ?? string.Empty;
                    TxtSrvStopMsg.Text = _config.Monitoring.ServerStoppedMessage ?? string.Empty;
                    TxtSrvRestartMsg.Text = _config.Monitoring.ServerRestartedMessage ?? string.Empty;
                    TxtSrvCrashMsg.Text = _config.Monitoring.ServerCrashedMessage ?? string.Empty;
                }

                // ---- Faction ----
                if (_config.Faction != null)
                {
                    ChkFactionEnabled.IsChecked = _config.Faction.Enabled;
                    ChkAutoCreateChannels.IsChecked = _config.Faction.AutoCreateChannels;
                    ChkAutoCreateForum.IsChecked = _config.Faction.AutoCreateForum;
                    ChkAutoCreateVoice.IsChecked = _config.Faction.AutoCreateVoice;
                    ChkFactionGlobalFallback.IsChecked = _config.Faction.FactionDiscordToGlobalFallback;
                }

                // ---- Storage ----
                if (_config.DataStorage != null)
                {
                    ChkUseSqlite.IsChecked = _config.DataStorage.UseSQLite;
                    ChkSaveEventLogs.IsChecked = _config.DataStorage.SaveEventLogs;
                    ChkSaveDeathHistory.IsChecked = _config.DataStorage.SaveDeathHistory;
                    ChkSaveGlobalChat.IsChecked = _config.DataStorage.SaveGlobalChat;
                    ChkSaveFactionChat.IsChecked = _config.DataStorage.SaveFactionChat;
                    ChkSavePrivateChat.IsChecked = _config.DataStorage.SavePrivateChat;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[SETTINGS_UI] LoadConfig error: " + ex.Message);
                MessageBox.Show(
                    "Error loading settings: " + ex.Message,
                    "TDS Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // ============================================================
        // SAVE: UI -> config -> disk
        // ============================================================

        private void SaveConfig()
        {
            if (_config == null) return;

            try
            {
                // ---- General ----
                _config.Enabled = ChkEnabled.IsChecked == true;
                _config.Debug = ChkDebug.IsChecked == true;
                _config.ShowNpcActivity = ChkShowNpcActivity.IsChecked == true;
                _config.Language = CmbLanguage.SelectedItem != null ? CmbLanguage.SelectedItem.ToString() : "en-US";

                string rawIds = TxtAdminSteamIDs.Text ?? string.Empty;
                var idList = rawIds
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var parsedIds = new System.Collections.Generic.List<long>();
                foreach (var id in idList)
                {
                    long parsed;
                    if (long.TryParse(id.Trim(), out parsed))
                        parsedIds.Add(parsed);
                }
                _config.AdminSteamIDs = parsedIds.ToArray();

                _config.CleanupIntervalSeconds = ParseInt(TxtCleanupInterval.Text, 30);
                _config.DamageHistoryMaxSeconds = ParseInt(TxtDamageHistoryMax.Text, 15);

                // ---- Discord ----
                if (_config.Discord == null)
                    _config.Discord = new DiscordConfig();

                _config.Discord.BotToken = TxtBotToken.Text.Trim();
                _config.Discord.GuildID = ParseUlong(TxtGuildId.Text, 0);
                _config.Discord.BotPrefix = TxtBotPrefix.Text.Trim();
                _config.Discord.EnableDMNotifications = ChkEnableDm.IsChecked == true;
                _config.Discord.SyncIntervalSeconds = ParseInt(TxtSyncInterval.Text, 30);
                _config.Discord.VerificationCodeExpirationMinutes = ParseInt(TxtVerifyExpiry.Text, 15);
                _config.Discord.ChatChannelId = ParseUlong(TxtChatChannelId.Text, 0);
                _config.Discord.StatusChannelId = ParseUlong(TxtStatusChannelId.Text, 0);
                _config.Discord.SimSpeedChannelId = ParseUlong(TxtSimSpeedChannelId.Text, 0);
                _config.Discord.PlayerCountChannelId = ParseUlong(TxtPlayerCountChannelId.Text, 0);
                _config.Discord.StaffLog = ParseUlong(TxtStaffLogId.Text, 0);
                _config.Discord.AdminAlertChannelId = ParseUlong(TxtAdminAlertChannelId.Text, 0);
                _config.Discord.AdminBotChannelId = ParseUlong(TxtAdminBotChannelId.Text, 0);
                _config.Discord.FactionCategoryId = ParseUlong(TxtFactionCategoryId.Text, 0);
                _config.Discord.VerifiedRoleId = ParseUlong(TxtVerifiedRoleId.Text, 0);

                // ---- Chat ----
                if (_config.Chat == null)
                    _config.Chat = new ChatConfig();

                _config.Chat.Enabled = ChkChatEnabled.IsChecked == true;
                _config.Chat.ServerToDiscord = ChkServerToDiscord.IsChecked == true;
                _config.Chat.BotToGame = ChkBotToGame.IsChecked == true;
                _config.Chat.UseFactionChat = ChkUseFactionChat.IsChecked == true;
                _config.Chat.StripEmojisForInGameChat = ChkStripEmojis.IsChecked == true;
                _config.Chat.GameToDiscordFormat = TxtGameToDiscord.Text;
                _config.Chat.DiscordToGameFormat = TxtDiscordToGame.Text;
                _config.Chat.ConnectMessage = TxtConnectMsg.Text;
                _config.Chat.JoinMessage = TxtJoinMsg.Text;
                _config.Chat.LeaveMessage = TxtLeaveMsg.Text;
                _config.Chat.FactionChatFormat = TxtFactionChatFormat.Text;
                _config.Chat.FactionDiscordFormat = TxtFactionDiscordFormat.Text;
                _config.Chat.GlobalColor = TxtGlobalColor.Text.Trim();
                _config.Chat.FactionColor = TxtFactionColor.Text.Trim();
                _config.Chat.EnableModeration = ChkEnableModeration.IsChecked == true;
                _config.Chat.MaxWarningsBeforeMute = ParseInt(TxtMaxWarnings.Text, 3);
                _config.Chat.MuteDurationMinutes = ParseInt(TxtMuteDuration.Text, 10);
                _config.Chat.MaxMutesBeforeKick = ParseInt(TxtMaxMutes.Text, 2);
                _config.Chat.AdminLogChannelId = ParseUlong(TxtChatAdminLogId.Text, 0);

                // ---- Deaths ----
                if (_config.Death == null)
                    _config.Death = new DeathConfig();

                _config.Death.Enabled = ChkDeathEnabled.IsChecked == true;
                _config.Death.LogToDiscord = ChkDeathLogToDiscord.IsChecked == true;
                _config.Death.AnnounceInGame = ChkDeathAnnounceIngame.IsChecked == true;
                _config.Death.DetectRetaliation = ChkDetectRetaliation.IsChecked == true;
                _config.Death.EnableLocationZones = ChkEnableLocationZones.IsChecked == true;
                _config.Death.GridDetectionEnabled = ChkGridDetection.IsChecked == true;
                _config.Death.ShowGridName = ChkShowGridName.IsChecked == true;
                _config.Death.RetaliationWindowMinutes = ParseInt(TxtRetaliationWindow.Text, 60);
                _config.Death.OldRevengeWindowHours = ParseInt(TxtOldRevengeWindow.Text, 24);
                _config.Death.MessageDeduplicationWindowSeconds = ParseInt(TxtDedupWindow.Text, 3);
                _config.Death.InnerSystemMaxKm = ParseDouble(TxtInnerSystemKm.Text, 5000.0);
                _config.Death.OuterSpaceMaxKm = ParseDouble(TxtOuterSpaceKm.Text, 10000.0);
                _config.Death.PlanetProximityMultiplier = ParseDouble(TxtPlanetProxMult.Text, 3.0);
                _config.Death.DeathMessageEmotes = TxtDeathEmotes.Text;

                // ---- Monitoring ----
                if (_config.Monitoring == null)
                    _config.Monitoring = new MonitoringConfig();

                _config.Monitoring.Enabled = ChkMonEnabled.IsChecked == true;
                _config.Monitoring.EnableSimSpeedMonitoring = ChkSimSpeedMon.IsChecked == true;
                _config.Monitoring.EnableSimSpeedAlerts = ChkSimSpeedAlerts.IsChecked == true;
                _config.Monitoring.SimSpeedThreshold = ParseFloat(TxtSimSpeedThreshold.Text, 0.6f);
                _config.Monitoring.SimSpeedAlertCooldownSeconds = ParseInt(TxtSimSpeedCooldown.Text, 1200);
                _config.Monitoring.SimSpeedChannelNameFormat = TxtSimSpeedChannelFormat.Text;
                _config.Monitoring.SimSpeedAlertMessage = TxtSimSpeedAlertMsg.Text;
                _config.Monitoring.EnablePlayerCountMonitoring = ChkPlayerCountMon.IsChecked == true;
                _config.Monitoring.EnablePlayerCountAlerts = ChkPlayerCountAlerts.IsChecked == true;
                _config.Monitoring.StatusUpdateIntervalSeconds = ParseInt(TxtStatusInterval.Text, 30);
                _config.Monitoring.PlayerCountChannelNameFormat = TxtPlayerCountChannelFormat.Text;
                _config.Monitoring.PlayerCountAlertThreshold = ParseInt(TxtPlayerCountThreshold.Text, 10);
                _config.Monitoring.EnableAdminAlerts = ChkAdminAlerts.IsChecked == true;
                _config.Monitoring.ServerStartedMessage = TxtSrvStartMsg.Text;
                _config.Monitoring.ServerStoppedMessage = TxtSrvStopMsg.Text;
                _config.Monitoring.ServerRestartedMessage = TxtSrvRestartMsg.Text;
                _config.Monitoring.ServerCrashedMessage = TxtSrvCrashMsg.Text;

                // ---- Faction ----
                if (_config.Faction == null)
                    _config.Faction = new FactionConfig();

                _config.Faction.Enabled = ChkFactionEnabled.IsChecked == true;
                _config.Faction.AutoCreateChannels = ChkAutoCreateChannels.IsChecked == true;
                _config.Faction.AutoCreateForum = ChkAutoCreateForum.IsChecked == true;
                _config.Faction.AutoCreateVoice = ChkAutoCreateVoice.IsChecked == true;
                _config.Faction.FactionDiscordToGlobalFallback = ChkFactionGlobalFallback.IsChecked == true;

                // ---- Storage ----
                if (_config.DataStorage != null)
                    _config.DataStorage = new DataStorageConfig();

                _config.DataStorage.UseSQLite = ChkUseSqlite.IsChecked == true;
                _config.DataStorage.SaveEventLogs = ChkSaveEventLogs.IsChecked == true;
                _config.DataStorage.SaveDeathHistory = ChkSaveDeathHistory.IsChecked == true;
                _config.DataStorage.SaveGlobalChat = ChkSaveGlobalChat.IsChecked == true;
                _config.DataStorage.SaveFactionChat = ChkSaveFactionChat.IsChecked == true;
                _config.DataStorage.SavePrivateChat = ChkSavePrivateChat.IsChecked == true;

                _config.Save();

                Lang.Load(_config.Language ?? "en-US", MainConfig.GetConfigDirectory());

                LoggerUtil.LogSuccess("[SETTINGS_UI] Configuration saved via Torch GUI");
                MessageBox.Show(
                    "Settings saved successfully.\nSome changes (bot token, sync interval) require a plugin restart.",
                    "TDS Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[SETTINGS_UI] SaveConfig error: " + ex.Message);
                MessageBox.Show(
                    "Error saving settings: " + ex.Message,
                    "TDS Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // ============================================================
        // BUTTON HANDLERS
        // ============================================================

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            _config = MainConfig.Load();
            PopulateLanguages();
            LoadConfig();
            LoggerUtil.LogInfo("[SETTINGS_UI] Configuration reloaded from file");
        }

        // ============================================================
        // PARSE HELPERS
        // ============================================================

        private static int ParseInt(string text, int fallback)
        {
            int result;
            return int.TryParse(text, out result) ? result : fallback;
        }

        private static ulong ParseUlong(string text, ulong fallback)
        {
            ulong result;
            return ulong.TryParse(text, out result) ? result : fallback;
        }

        private static float ParseFloat(string text, float fallback)
        {
            float result;
            return float.TryParse(text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out result) ? result : fallback;
        }

        private static double ParseDouble(string text, double fallback)
        {
            double result;
            return double.TryParse(text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out result) ? result : fallback;
        }
    }
}