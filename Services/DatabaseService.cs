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

        public DatabaseService()
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
        }

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

        public FactionModel GetFaction(int factionID)
        {
            return _data.Factions.FirstOrDefault(f => f.FactionID == factionID);
        }

        public List<FactionModel> GetAllFactions()
        {
            return new List<FactionModel>(_data.Factions);
        }

        public void SavePlayer(PlayerModel player)
        {
            lock (_lock)
            {
                var existing = _data.Players.FirstOrDefault(p => p.PlayerID == player.PlayerID);
                if (existing != null)
                {
                    existing.OriginalNick = player.OriginalNick;
                    existing.SyncedNick = player.SyncedNick;
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

        public void LogEvent(EventLogModel evt)
        {
            lock (_lock)
            {
                _data.EventLogs.Add(evt);
                SaveToXml();
            }
        }

        public void LogDeath(long killerSteamID, long victimSteamID, string deathType)
        {
            lock (_lock)
            {
                _data.DeathHistory.Add(new DeathHistoryModel
                {
                    KillerSteamID = killerSteamID,
                    VictimSteamID = victimSteamID,
                    DeathTime = DateTime.UtcNow,
                    DeathType = deathType
                });
                SaveToXml();
            }
        }

        public DeathHistoryModel GetLastKill(long killerSteamID, long victimSteamID)
        {
            return _data.DeathHistory
                .Where(d => d.KillerSteamID == killerSteamID && d.VictimSteamID == victimSteamID)
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
        
        /* ADD verification methods */
        /// <summary>
        /// Save or update a verification
        /// </summary>
        public void SaveVerification(VerificationModel verification)
        {
            lock (_lock)
            {
                var existing = _data.Verifications.FirstOrDefault(v => v.SteamID == verification.SteamID);
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

        /* Update: Services/DatabaseService.cs - ADD verification methods */
        /// <summary>
        /// Get verification by Steam ID
        /// </summary>
        public VerificationModel GetVerification(long steamID)
        {
            return _data.Verifications.FirstOrDefault(v => v.SteamID == steamID);
        }

        /// <summary>
        /// Get verification by code
        /// </summary>
        public VerificationModel GetVerificationByCode(string code)
        {
            return _data.Verifications.FirstOrDefault(v => v.VerificationCode == code);
        }

        /// <summary>
        /// Get all verifications
        /// </summary>
        public List<VerificationModel> GetAllVerifications()
        {
            return new List<VerificationModel>(_data.Verifications);
        }

        /// <summary>
        /// Delete verification
        /// </summary>
        public void DeleteVerification(long steamID)
        {
            lock (_lock)
            {
                _data.Verifications.RemoveAll(v => v.SteamID == steamID);
                SaveToXml();
            }
        }

        /// <summary>
        /// Save verification history entry
        /// </summary>
        public void SaveVerificationHistory(VerificationHistoryModel entry)
        {
            lock (_lock)
            {
                _data.VerificationHistory.Add(entry);
                SaveToXml();
            }
        }

        /// <summary>
        /// Get verification history for a Steam ID
        /// </summary>
        public List<VerificationHistoryModel> GetVerificationHistory(long steamID)
        {
            return _data.VerificationHistory
                .Where(v => v.SteamID == steamID)
                .OrderByDescending(v => v.VerifiedAt)
                .ToList();
        }


    }
}