// File: Config\MainConfig.cs
using System.Collections.Generic;
using System.Xml.Serialization;
using Torch;

namespace mamba.TorchDiscordSync.Config
{
    public class MainConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public bool Debug { get; set; } = true;
        public int SyncIntervalSeconds { get; set; } = 30;

        [XmlArray("AdminSteamIDs")]
        [XmlArrayItem("SteamID")]
        public long[] AdminSteamIDs { get; set; } = new long[0];

        public DiscordConfig Discord { get; set; } = new DiscordConfig();
        public ChatConfig Chat { get; set; } = new ChatConfig();
        public DeathConfig Death { get; set; } = new DeathConfig();
        public MonitoringConfig Monitoring { get; set; } = new MonitoringConfig();
        public FactionConfig Faction { get; set; } = new FactionConfig();
        public PlayerTrackingConfig PlayerTracking { get; set; } = new PlayerTrackingConfig();

        public static MainConfig Load() { return new MainConfig(); }
    }

    public class DiscordConfig : ViewModel
    {
        public string BotToken { get; set; } = "TOKEN";
        public ulong GuildID { get; set; } = 0;
        public string BotPrefix { get; set; } = "!";
        public bool EnableDMNotifications { get; set; } = true;
        public int VerificationCodeExpirationMinutes { get; set; } = 15;
        public ulong ChatChannelId { get; set; } = 0;
        public ulong StaffLog { get; set; } = 0;
        public ulong StatusChannelId { get; set; } = 0;
        public ulong SimSpeedChannelId { get; set; } = 0;
        public ulong PlayersOnlineChannelId { get; set; } = 0;
        public ulong FactionCategoryId { get; set; } = 0;
    }

    public class ChatConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public bool BotToGame { get; set; } = true;
        public bool ServerToDiscord { get; set; } = true;
        public string GameToDiscordFormat { get; set; } = ":rocket: **{p}**: {msg}";
        public string DiscordToGameFormat { get; set; } = "[Discord] {p}: {msg}";
        public string ConnectMessage { get; set; } = ":key: {p} connected to server";
        public string JoinMessage { get; set; } = ":sunny: {p} joined the server";
        public string LeaveMessage { get; set; } = ":new_moon: {p} left the server";
        public bool UseFactionChat { get; set; } = false;
        public string FactionChatFormat { get; set; } = ":ledger: **{p}**: {msg}";
        public string FactionDiscordFormat { get; set; } = "[SE-Faction] {p}: {msg}";
        public string GlobalColor { get; set; } = "White";
        public string FactionColor { get; set; } = "Green";
    }

    public class DeathConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public bool LogToDiscord { get; set; } = true;
        public bool AnnounceInGame { get; set; } = true;
        public bool DetectRetaliation { get; set; } = true;
        public int RetaliationWindowMinutes { get; set; } = 60;
        public int OldRevengeWindowHours { get; set; } = 24;
    }

    public class MonitoringConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public float SimThresh { get; set; } = 0.6f;
        public int CheckIntervalSeconds { get; set; } = 30;
        public bool EnableSimSpeedMonitoring { get; set; } = true;
        public string SimSpeedAlertMessage { get; set; } = "@here Sim speed low!";
        public int SimSpeedAlertCooldownSeconds { get; set; } = 1200;
        public bool UseStatusUpdates { get; set; } = true;
        public int StatusUpdateIntervalSeconds { get; set; } = 5000;
        public string StatusMessageFormat { get; set; } = "{p} players | SimSpeed {ss}";
        public string ServerStartedMessage { get; set; } = ":white_check_mark: Server Started!";
        public string ServerStoppedMessage { get; set; } = ":x: Server Stopped!";
        public string ServerRestartedMessage { get; set; } = ":arrows_counterclockwise: Server Restarted!";
        public string SimSpeedChannelNameFormat { get; set; } = "simspeed-{ss}";
    }

    public class FactionConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public bool AutoCreateChannels { get; set; } = true;
        public bool AutoCreateVoice { get; set; } = true;
        public ulong CategoryId { get; set; } = 0;
    }

    public class PlayerTrackingConfig : ViewModel
    {
        public bool Enabled { get; set; } = true;
        public bool TrackOnlineStatus { get; set; } = true;
        public int UpdateIntervalSeconds { get; set; } = 30;
        public string PlayersOnlineFormat { get; set; } = "{total} registered | {online} online";
    }
}