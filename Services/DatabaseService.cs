// Services/DatabaseService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    public class DatabaseService
    {
        private readonly string _xmlPath;
        private RootDataModel _data;
        private readonly object _lock = new object();

        // ============================================================
        // VERIFICATIONPLAYERS.XML FIELDS
        // ============================================================
        private readonly string _verificationPlayersPath;
        private VerificationPlayersData _verificationPlayersData;

        /// <summary>
        /// Init database service, load data from XML or create new if not exists
        /// </summary>
        /// <param name="configPath"></param>
        public DatabaseService(string configPath = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var instanceDir = Path.Combine(baseDir, "Instance");
            if (!Directory.Exists(instanceDir))
                Directory.CreateDirectory(instanceDir);

            var folder = Path.Combine(instanceDir, "mambaTorchDiscordSync");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _xmlPath = Path.Combine(folder, "MambaTorchDiscordSyncData.xml");

            if (File.Exists(_xmlPath))
                LoadFromXml();
            else
                _data = new RootDataModel();

            // ============================================================
            // INIT VERIFICATIONPLAYERS.XML PATH
            // ============================================================
            _verificationPlayersPath = Path.Combine(folder, "VerificationPlayers.xml");
            LoadVerificationPlayersFromXml();
        }

        // ============================================================
        // MAIN DATABASE METHODS
        // ============================================================

        /// <summary>
        /// Loads data from XML file into memory. If file is missing or corrupted, initializes empty data model.
        /// </summary>
        private void LoadFromXml()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(RootDataModel));
                using (FileStream fs = new FileStream(_xmlPath, FileMode.Open))
                {
                    _data = (RootDataModel)serializer.Deserialize(fs);
                }
                if (_data == null)
                    _data = new RootDataModel();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Failed to load XML: {ex.Message}");
                _data = new RootDataModel();
            }
        }

        /// <summary>
        /// Saves current in-memory data to XML file. This should be called after any changes to ensure persistence.
        /// </summary>
        public void SaveToXml()
        {
            lock (_lock)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(RootDataModel));
                    using (FileStream fs = new FileStream(_xmlPath, FileMode.Create))
                    {
                        serializer.Serialize(fs, _data);
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"Failed to save XML: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves or updates a faction in the database. If the faction already exists (based on FactionID), it will be updated. Otherwise, a new faction will be added.
        /// </summary>
        /// <param name="faction"></param>
        public void SaveFaction(FactionModel faction)
        {
            lock (_lock)
            {
                var existing = _data.Factions.FirstOrDefault(f => f.FactionID == faction.FactionID);
                if (existing != null)
                {
                    existing.Tag = faction.Tag;
                    existing.Name = faction.Name;
                    existing.DiscordRoleID = faction.DiscordRoleID;
                    existing.DiscordChannelID = faction.DiscordChannelID;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Players = faction.Players;
                }
                else
                {
                    faction.CreatedAt = DateTime.UtcNow;
                    faction.UpdatedAt = DateTime.UtcNow;
                    _data.Factions.Add(faction);
                }
                SaveToXml();
            }
        }

        /// <summary>
        /// Retrieves a faction by its ID. Returns null if not found.
        /// </summary>
        /// <param name="factionID"></param>
        /// <returns></returns>
        public FactionModel GetFaction(int factionID)
        {
            return _data.Factions.FirstOrDefault(f => f.FactionID == factionID);
        }

        public List<FactionModel> GetAllFactions()
        {
            return new List<FactionModel>(_data.Factions);
        }

        /// <summary>
        /// Saves or updates a player in the database. If the player already exists (based on SteamID), it will be updated. Otherwise, a new player will be added.
        /// </summary>
        /// <param name="player"></param>
        public void SavePlayer(PlayerModel player)
        {
            lock (_lock)
            {
                var existing = _data.Players.FirstOrDefault(p => p.SteamID == player.SteamID);
                if (existing != null)
                {
                    existing.OriginalNick = player.OriginalNick;
                    existing.SyncedNick = player.SyncedNick;
                    existing.SteamID = player.SteamID;
                    existing.FactionID = player.FactionID;
                    existing.DiscordUserID = player.DiscordUserID;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    player.CreatedAt = DateTime.UtcNow;
                    player.UpdatedAt = DateTime.UtcNow;
                    _data.Players.Add(player);
                }
                SaveToXml();
            }
        }

        /// <summary>
        /// Retrieves a player by their SteamID. Returns null if not found.
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns></returns>

        public PlayerModel GetPlayerBySteamID(long steamID)
        {
            return _data.Players.FirstOrDefault(p => p.SteamID == steamID);
        }

        public void LogEvent(EventLogModel evt)
        {
            lock (_lock)
            {
                _data.EventLogs.Add(evt);
                SaveToXml();
            }
        }

        /// <summary>
        /// Logs a player death event to the database
        /// </summary>
        /// <param name="killerSteamID"></param>
        /// <param name="victimSteamID"></param>
        /// <param name="deathType"></param>
        /// <param name="weapon"></param>
        /// <param name="location"></param>
        public void LogDeath(
            long killerSteamID,
            long victimSteamID,
            string deathType,
            string weapon = null,
            string location = null
        )
        {
            lock (_lock)
            {
                var entry = new DeathHistoryModel
                {
                    KillerSteamID = killerSteamID,
                    VictimSteamID = victimSteamID,
                    DeathTime = DateTime.UtcNow,
                    DeathType = deathType,
                    Weapon = weapon,
                    Location = location,
                };
                _data.DeathHistory.Add(entry);
                SaveToXml();
            }
        }

        public DeathHistoryModel GetLastKill(long killerSteamID, long victimSteamID)
        {
            return _data
                .DeathHistory.Where(d =>
                    d.KillerSteamID == killerSteamID && d.VictimSteamID == victimSteamID
                )
                .OrderByDescending(d => d.DeathTime)
                .FirstOrDefault();
        }

        public void ClearAllData()
        {
            lock (_lock)
            {
                _data = new RootDataModel();
                SaveToXml();
            }
        }

        // ============================================================
        // VERIFICATION METHODS - U GLAVNOJ BAZI (LEGACY)
        // ============================================================
        public void SaveVerification(VerificationModel verification)
        {
            lock (_lock)
            {
                var existing = _data.Verifications.FirstOrDefault(v =>
                    v.SteamID == verification.SteamID
                );
                if (existing != null)
                {
                    existing.VerificationCode = verification.VerificationCode;
                    existing.CodeGeneratedAt = verification.CodeGeneratedAt;
                    existing.DiscordUsername = verification.DiscordUsername;
                    existing.IsVerified = verification.IsVerified;
                    existing.VerifiedAt = verification.VerifiedAt;
                    existing.DiscordUserID = verification.DiscordUserID;
                }
                else
                {
                    _data.Verifications.Add(verification);
                }
                SaveToXml();
            }
        }

        public VerificationModel GetVerification(long steamID) =>
            _data.Verifications.FirstOrDefault(v => v.SteamID == steamID);

        public VerificationModel GetVerificationByCode(string code) =>
            _data.Verifications.FirstOrDefault(v => v.VerificationCode == code);

        public List<VerificationModel> GetAllVerifications() =>
            new List<VerificationModel>(_data.Verifications);

        public void DeleteVerification(long steamID)
        {
            lock (_lock)
            {
                _data.Verifications.RemoveAll(v => v.SteamID == steamID);
                SaveToXml();
            }
        }

        public void SaveVerificationHistory(VerificationHistoryModel entry)
        {
            lock (_lock)
            {
                _data.VerificationHistory.Add(entry);
                SaveToXml();
            }
        }

        public List<VerificationHistoryModel> GetVerificationHistory(long steamID)
        {
            return _data
                .VerificationHistory.Where(v => v.SteamID == steamID)
                .OrderByDescending(v => v.VerifiedAt)
                .ToList();
        }

        // ============================================================
        // JAVNE METODE ZA VERIFICATIONPLAYERS.XML
        // ============================================================

        private void LoadVerificationPlayersFromXml()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_verificationPlayersPath))
                    {
                        var serializer = new XmlSerializer(typeof(VerificationPlayersData));
                        using (var stream = new FileStream(_verificationPlayersPath, FileMode.Open))
                        {
                            _verificationPlayersData = (VerificationPlayersData)
                                serializer.Deserialize(stream);
                        }
                        LoggerUtil.LogSuccess(
                            $"[DB] Loaded VerificationPlayers.xml - {_verificationPlayersData.PendingVerifications.Count} pending, {_verificationPlayersData.VerifiedPlayers.Count} verified"
                        );
                    }
                    else
                    {
                        _verificationPlayersData = new VerificationPlayersData();
                        SaveVerificationPlayersToXml();
                        LoggerUtil.LogInfo("[DB] Created new VerificationPlayers.xml");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError(
                        $"[DB] Error loading VerificationPlayers.xml: {ex.Message}"
                    );
                    _verificationPlayersData = new VerificationPlayersData();
                }
            }
        }

        private void SaveVerificationPlayersToXml()
        {
            lock (_lock)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(VerificationPlayersData));
                    using (var stream = new FileStream(_verificationPlayersPath, FileMode.Create))
                    {
                        serializer.Serialize(stream, _verificationPlayersData);
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"[DB] Error saving VerificationPlayers.xml: {ex.Message}");
                }
            }
        }

        public void AddPendingVerification(
            long steamID,
            string discordUsername,
            string verificationCode,
            int expirationMinutes = 15
        )
        {
            lock (_lock)
            {
                // Ukloni ako već postoji
                _verificationPlayersData.PendingVerifications.RemoveAll(p => p.SteamID == steamID);

                var pending = new PendingVerification
                {
                    SteamID = steamID,
                    DiscordUsername = discordUsername,
                    VerificationCode = verificationCode,
                    CodeGeneratedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
                };

                _verificationPlayersData.PendingVerifications.Add(pending);
                SaveVerificationPlayersToXml();
                LoggerUtil.LogDebug($"[DB] Added pending verification for SteamID {steamID}");
            }
        }

        public void MarkAsVerified(long steamID, string discordUsername, ulong discordUserID)
        {
            lock (_lock)
            {
                // Ukloni sa pending liste
                _verificationPlayersData.PendingVerifications.RemoveAll(p => p.SteamID == steamID);

                // Ukloni ako postoji na verified listi
                _verificationPlayersData.VerifiedPlayers.RemoveAll(v => v.SteamID == steamID);

                // Dodaj na verified listu
                var verified = new VerifiedPlayer
                {
                    SteamID = steamID,
                    DiscordUsername = discordUsername,
                    DiscordUserID = discordUserID,
                    VerifiedAt = DateTime.UtcNow,
                };

                _verificationPlayersData.VerifiedPlayers.Add(verified);
                SaveVerificationPlayersToXml();
                LoggerUtil.LogDebug($"[DB] Marked SteamID {steamID} as verified");
            }
        }

        public PendingVerification GetPendingVerification(long steamID)
        {
            lock (_lock)
            {
                var pending = _verificationPlayersData.PendingVerifications.Find(p =>
                    p.SteamID == steamID
                );

                // Ako je expired, obriši
                if (pending != null && pending.ExpiresAt < DateTime.UtcNow)
                {
                    _verificationPlayersData.PendingVerifications.Remove(pending);
                    SaveVerificationPlayersToXml();
                    return null;
                }

                return pending;
            }
        }

        public VerifiedPlayer GetVerifiedPlayer(long steamID)
        {
            lock (_lock)
            {
                return _verificationPlayersData.VerifiedPlayers.Find(v => v.SteamID == steamID);
            }
        }

        public VerifiedPlayer GetVerifiedPlayerByDiscordID(ulong discordUserID)
        {
            lock (_lock)
            {
                return _verificationPlayersData.VerifiedPlayers.Find(v =>
                    v.DiscordUserID == discordUserID
                );
            }
        }

        public List<PendingVerification> GetAllPendingVerifications()
        {
            lock (_lock)
            {
                return new List<PendingVerification>(_verificationPlayersData.PendingVerifications);
            }
        }

        public List<VerifiedPlayer> GetAllVerifiedPlayers()
        {
            lock (_lock)
            {
                return new List<VerifiedPlayer>(_verificationPlayersData.VerifiedPlayers);
            }
        }

        public void DeletePendingVerification(long steamID)
        {
            lock (_lock)
            {
                _verificationPlayersData.PendingVerifications.RemoveAll(p => p.SteamID == steamID);
                SaveVerificationPlayersToXml();
                LoggerUtil.LogDebug($"[DB] Deleted pending verification for SteamID {steamID}");
            }
        }

        public void DeleteVerifiedPlayer(long steamID)
        {
            lock (_lock)
            {
                _verificationPlayersData.VerifiedPlayers.RemoveAll(v => v.SteamID == steamID);
                SaveVerificationPlayersToXml();
                LoggerUtil.LogDebug($"[DB] Deleted verified player SteamID {steamID}");
            }
        }
    }

    // ============================================================
    // XML KLASE ZA VERIFICATIONPLAYERS.XML
    // ============================================================

    [XmlRoot("VerificationPlayers")]
    public class VerificationPlayersData
    {
        [XmlArray("PendingVerifications")]
        [XmlArrayItem("Pending")]
        public List<PendingVerification> PendingVerifications { get; set; } =
            new List<PendingVerification>();

        [XmlArray("VerifiedPlayers")]
        [XmlArrayItem("Verified")]
        public List<VerifiedPlayer> VerifiedPlayers { get; set; } = new List<VerifiedPlayer>();
    }

    public class PendingVerification
    {
        [XmlElement]
        public long SteamID { get; set; }

        [XmlElement]
        public string DiscordUsername { get; set; }

        [XmlElement]
        public string VerificationCode { get; set; }

        [XmlElement]
        public DateTime CodeGeneratedAt { get; set; }

        [XmlElement]
        public DateTime ExpiresAt { get; set; }
    }

    public class VerifiedPlayer
    {
        [XmlElement]
        public long SteamID { get; set; }

        [XmlElement]
        public string DiscordUsername { get; set; }

        [XmlElement]
        public ulong DiscordUserID { get; set; }

        [XmlElement]
        public DateTime VerifiedAt { get; set; }
    }
}
