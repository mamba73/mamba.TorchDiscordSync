// Config/MainConfig.cs
using System;
using System.IO;
using System.Xml.Serialization;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Config
{
    [XmlRoot("MainConfig")]
    public class MainConfig
    {
        // Static field for instance-specific config directory name
        private static readonly string CONFIG_DIR_NAME = "mambaTorchDiscordSync";

        // Property to get correct config path based on Torch instance directory
        private static string ConfigPath
        {
            get
            {
                // Get instance directory from environment or use default
                string instancePath = GetInstancePath();
                string pluginConfigDir = Path.Combine(instancePath, CONFIG_DIR_NAME);

                // Ensure directory exists
                if (!Directory.Exists(pluginConfigDir))
                {
                    try
                    {
                        Directory.CreateDirectory(pluginConfigDir);
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            "Failed to create config directory "
                                + pluginConfigDir
                                + ": "
                                + ex.Message
                        );
                    }
                }

                return Path.Combine(pluginConfigDir, "MainConfig.xml");
            }
        }

        // Method to determine correct instance path
        private static string GetInstancePath()
        {
            // Try to get from environment variable (set by Torch)
            string instancePath = Environment.GetEnvironmentVariable("TORCH_INSTANCE_PATH");

            // Fallback to current directory structure
            if (string.IsNullOrEmpty(instancePath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                instancePath = Path.Combine(baseDir, "Instance");
            }

            return instancePath;
        }

        // ========== CORE SETTINGS ==========
        [XmlElement]
        public bool Enabled { get; set; }

        [XmlElement]
        public bool Debug { get; set; }

        [XmlElement]
        public int SyncIntervalSeconds { get; set; }

        [XmlArray("AdminSteamIDs")]
        [XmlArrayItem("SteamID")]
        public long[] AdminSteamIDs { get; set; }

        // ========== DISCORD BOT SETTINGS ==========
        [XmlElement]
        public DiscordConfig Discord { get; set; }

        // ========== CHAT SYNC SETTINGS ==========
        [XmlElement]
        public ChatConfig Chat { get; set; }

        // ========== DEATH LOGGING SETTINGS ==========
        [XmlElement]
        public DeathConfig Death { get; set; }

        // ========== SERVER MONITORING SETTINGS ==========
        [XmlElement]
        public MonitoringConfig Monitoring { get; set; }

        // ========== FACTION SETTINGS ==========
        [XmlElement]
        public FactionConfig Faction { get; set; }

        public MainConfig()
        {
            Enabled = true;
            Debug = false;
            SyncIntervalSeconds = 30;
            AdminSteamIDs = new long[0];
            Discord = new DiscordConfig();
            Chat = new ChatConfig();
            Death = new DeathConfig();
            Monitoring = new MonitoringConfig();
            Faction = new FactionConfig();
        }

        // Updated Load method to use correct path
        public static MainConfig Load()
        {
            try
            {
                // Use the updated ConfigPath property
                if (File.Exists(ConfigPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(MainConfig));
                    using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
                    {
                        MainConfig config = (MainConfig)serializer.Deserialize(fs);
                        return config != null ? config : new MainConfig();
                    }
                }
                else
                {
                    MainConfig config = new MainConfig();
                    config.Save();
                    return config;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Failed to load MainConfig: " + ex.Message);
            }
            return new MainConfig();
        }

        // Updated Save method to use correct path
        public void Save()
        {
            try
            {
                // Use the updated ConfigPath property
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                XmlSerializer serializer = new XmlSerializer(typeof(MainConfig));
                using (FileStream fs = new FileStream(ConfigPath, FileMode.Create))
                {
                    serializer.Serialize(fs, this);
                }
                LoggerUtil.LogSuccess("MainConfig saved successfully");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Failed to save MainConfig: " + ex.Message);
            }
        }
    }

    // ========== DISCORD CONFIGURATION ==========
    [XmlType("DiscordConfig")]
    public class DiscordConfig
    {
        [XmlElement]
        public string BotToken { get; set; }

        [XmlElement]
        public ulong GuildID { get; set; }

        [XmlElement]
        public string BotPrefix { get; set; }

        [XmlElement]
        public bool EnableDMNotifications { get; set; }

        [XmlElement]
        public int VerificationCodeExpirationMinutes { get; set; }

        [XmlElement]
        public ulong ChatChannelId { get; set; }

        [XmlElement]
        public ulong StaffLog { get; set; }

        [XmlElement]
        public ulong StatusChannelId { get; set; }
        
        [XmlElement]
        public ulong SimSpeedChannelId { get; set; }

        [XmlElement]
        public ulong PlayerCountChannelId { get; set; }


        public DiscordConfig()
        {
            BotToken = "YOUR_BOT_TOKEN";
            GuildID = 0;
            BotPrefix = "!";
            EnableDMNotifications = true;
            VerificationCodeExpirationMinutes = 15;
            ChatChannelId = 0;
            StaffLog = 0;
            StatusChannelId = 0;
            SimSpeedChannelId = 0;
            PlayerCountChannelId = 0;
        }
    }

    // ========== CHAT SYNCHRONIZATION CONFIGURATION ==========
    [XmlType("ChatConfig")]
    public class ChatConfig
    {
        [XmlElement]
        public bool EnableModeration { get; set; }

        [XmlElement]
        public string[] BlacklistedWords { get; set; }

        [XmlElement]
        public int MaxWarningsBeforeMute { get; set; }

        [XmlElement]
        public int MuteDurationMinutes { get; set; }

        [XmlElement]
        public int MaxMutesBeforeKick { get; set; }

        [XmlElement]
        public string WarningMessage { get; set; }

        [XmlElement]
        public string MuteMessage { get; set; }

        [XmlElement]
        public string KickMessage { get; set; }

        [XmlElement]
        public ulong AdminLogChannelId { get; set; }

        [XmlElement]
        public bool Enabled { get; set; }

        [XmlElement]
        public bool BotToGame { get; set; }

        [XmlElement]
        public bool ServerToDiscord { get; set; }

        [XmlElement]
        public string GameToDiscordFormat { get; set; }

        [XmlElement]
        public string DiscordToGameFormat { get; set; }

        [XmlElement]
        public string ConnectMessage { get; set; }

        [XmlElement]
        public string JoinMessage { get; set; }

        [XmlElement]
        public string LeaveMessage { get; set; }

        [XmlElement]
        public bool UseFactionChat { get; set; }

        [XmlElement]
        public string FactionChatFormat { get; set; }

        [XmlElement]
        public string FactionDiscordFormat { get; set; }

        [XmlElement]
        public string GlobalColor { get; set; }

        [XmlElement]
        public string FactionColor { get; set; }

        [XmlElement]
        public bool StripEmojisForInGameChat { get; set; }

        public ChatConfig()
        {
            Enabled = false;
            BotToGame = false;
            ServerToDiscord = false;
            GameToDiscordFormat = ":rocket: **{p}**: {msg}";
            DiscordToGameFormat = "[Discord] {p}: {msg}";
            ConnectMessage = ":key: {p} connected to server";
            JoinMessage = ":sunny: {p} joined the server";
            LeaveMessage = ":new_moon: {p} left the server";
            UseFactionChat = false;
            FactionChatFormat = ":ledger: **{p}**: {msg}";
            FactionDiscordFormat = "[SE-Faction] {p}: {msg}";
            GlobalColor = "White";
            FactionColor = "Green";
            StripEmojisForInGameChat = true;
            EnableModeration = false;
            BlacklistedWords = new string[] { "hack", "cheat", "exploit", "http" };
            MaxWarningsBeforeMute = 3;
            MuteDurationMinutes = 10;
            MaxMutesBeforeKick = 2;
            WarningMessage = "?? Please avoid using inappropriate language.";
            MuteMessage =
                "?? You have been muted for {minutes} minutes due to repeated violations.";
            KickMessage = "?? You have been removed from the channel for repeated violations.";
            AdminLogChannelId = 0;
        }
    }

    // ========== DEATH LOGGING CONFIGURATION ==========
    [XmlType("DeathConfig")]
    public class DeathConfig
    {
        [XmlElement]
        public bool Enabled { get; set; }

        [XmlElement]
        public bool LogToDiscord { get; set; }

        [XmlElement]
        public bool AnnounceInGame { get; set; }

        [XmlElement]
        public bool DetectRetaliation { get; set; }

        [XmlElement]
        public int RetaliationWindowMinutes { get; set; }

        [XmlElement]
        public int OldRevengeWindowHours { get; set; }

        // NEW: Death Location Zones configuration
        [XmlElement]
        public bool EnableLocationZones { get; set; }

        [XmlElement]
        public bool GridDetectionEnabled { get; set; }

        [XmlElement]
        public bool ShowGridName { get; set; }

        [XmlElement]
        public string DeathMessageEmotes { get; set; }

        [XmlElement]
        public int MessageDeduplicationWindowSeconds { get; set; }

        // NEW: Location detection thresholds (in kilometers)
        [XmlElement]
        public double InnerSystemMaxKm { get; set; }

        [XmlElement]
        public double OuterSpaceMaxKm { get; set; }

        [XmlElement]
        public double PlanetProximityMultiplier { get; set; }

        public DeathConfig()
        {
            Enabled = false;
            LogToDiscord = false;
            AnnounceInGame = false;
            DetectRetaliation = false;
            RetaliationWindowMinutes = 60;
            OldRevengeWindowHours = 24;

            // Death Location Zones defaults
            EnableLocationZones = true;
            GridDetectionEnabled = true;
            ShowGridName = true;
            
            DeathMessageEmotes = "📢,⚔️,💀,🔥,⚡";
            MessageDeduplicationWindowSeconds = 3;

            // Location detection thresholds (in kilometers)
            InnerSystemMaxKm = 5000.0;
            OuterSpaceMaxKm = 10000.0;
            PlanetProximityMultiplier = 3.0;
        }
    }

    // ========== SERVER MONITORING CONFIGURATION ==========
    [XmlType("MonitoringConfig")]
    public class MonitoringConfig
    {
        [XmlElement]
        public bool Enabled { get; set; }

        [XmlElement]
        public float SimThresh { get; set; }

        [XmlElement]
        public int CheckIntervalSeconds { get; set; }

        [XmlElement]
        public bool EnableSimSpeedMonitoring { get; set; }

        [XmlElement]
        public string SimSpeedAlertMessage { get; set; }

        [XmlElement]
        public int SimSpeedAlertCooldownSeconds { get; set; }

        [XmlElement]
        public bool UseStatusUpdates { get; set; }

        [XmlElement]
        public int StatusUpdateIntervalSeconds { get; set; }

        [XmlElement]
        public string StatusMessageFormat { get; set; }

        [XmlElement]
        public string ServerStartedMessage { get; set; }

        [XmlElement]
        public string ServerStoppedMessage { get; set; }

        [XmlElement]
        public string ServerRestartedMessage { get; set; }

        public MonitoringConfig()
        {
            Enabled = false;
            SimThresh = 0.6f;
            CheckIntervalSeconds = 30;
            EnableSimSpeedMonitoring = false;
            SimSpeedAlertMessage = "@here Simulation speed has dropped below threshold!";
            SimSpeedAlertCooldownSeconds = 1200;
            UseStatusUpdates = false;
            StatusUpdateIntervalSeconds = 5000;
            StatusMessageFormat = "{p} players | SimSpeed {ss}";
            ServerStartedMessage = ":white_check_mark: Server Started!";
            ServerStoppedMessage = ":x: Server Stopped!";
            ServerRestartedMessage = ":arrows_counterclockwise: Server Restarted!";
        }
    }

    // ========== FACTION CONFIGURATION ==========
    [XmlType("FactionConfig")]
    public class FactionConfig
    {
        [XmlElement]
        public bool Enabled { get; set; }

        [XmlElement]
        public bool AutoCreateChannels { get; set; }

        [XmlElement]
        public bool AutoCreateVoice { get; set; }

        [XmlElement]
        public ulong CategoryId { get; set; }

        public FactionConfig()
        {
            Enabled = false;
            AutoCreateChannels = false;
            AutoCreateVoice = false;
            CategoryId = 0;
        }
    }
}