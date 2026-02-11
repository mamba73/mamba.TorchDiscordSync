using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using mamba.TorchDiscordSync.Plugin.Utils;

namespace mamba.TorchDiscordSync.Plugin.Services
{
    public class VerificationService
    {
        private readonly object _lock = new object();

        // existing dependencies (do NOT change constructor signature)
        private readonly object _storage;
        private readonly object _config;

        // NEW: XML file path for verified users persistence
        private readonly string _verifiedFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TorchDiscordSync_VerifiedUsers.xml");

        // NEW: pending verification codes in memory
        private readonly Dictionary<string, PendingVerification> _pendingCodes =
            new Dictionary<string, PendingVerification>();

        // NEW: verified users cache (SteamId -> DiscordId)
        private Dictionary<long, ulong> _verifiedUsers =
            new Dictionary<long, ulong>();

        public VerificationService(object storage, object config)
        {
            _storage = storage;
            _config = config;

            LoadVerifiedUsers(); // NEW: load verified users on startup
        }

        private class PendingVerification
        {
            public long SteamId;
            public string PlayerName;
            public string DiscordUsername;
            public DateTime ExpirationUtc;
        }

        // NEW: serializable container for XML
        public class VerifiedUserEntry
        {
            public long SteamId;
            public ulong DiscordId;
        }

        public string GenerateVerificationCode(
            long steamId,
            string playerName,
            string discordUsername,
            string gamePlayerName
        )
        {
            lock (_lock)
            {
                // NEW: block if already verified
                if (_verifiedUsers.ContainsKey(steamId))
                {
                    return null;
                }

                if (_pendingCodes.Values.Any(p => p.SteamId == steamId))
                    return null;

                string code = Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 6)
                    .ToUpperInvariant();

                _pendingCodes[code] = new PendingVerification
                {
                    SteamId = steamId,
                    PlayerName = playerName,
                    DiscordUsername = discordUsername,
                    ExpirationUtc = DateTime.UtcNow.AddMinutes(15)
                };

                LoggerUtil.LogDebug(
                    "[VERIFY] Pending verification created. SteamId=" + steamId + " Code=" + code
                ); // NEW

                return code;
            }
        }

        public Task<string> VerifyAsync(
            string code,
            ulong discordId,
            string discordUsername
        )
        {
            lock (_lock)
            {
                if (!_pendingCodes.ContainsKey(code))
                {
                    return Task.FromResult("Invalid or expired verification code.");
                }

                PendingVerification pending = _pendingCodes[code];

                if (pending.ExpirationUtc < DateTime.UtcNow)
                {
                    _pendingCodes.Remove(code);
                    return Task.FromResult("Verification code expired.");
                }

                // NEW: save verified link
                _verifiedUsers[pending.SteamId] = discordId;

                SaveVerifiedUsers(); // NEW: persist to XML

                _pendingCodes.Remove(code);

                LoggerUtil.LogInfo(
                    "[VERIFY] Verification completed. SteamId="
                        + pending.SteamId
                        + " DiscordId="
                        + discordId
                ); // NEW

                return Task.FromResult(
                    "Verification successful. Your account is now linked."
                );
            }
        }

        public bool RemoveVerification(long steamId, string reason)
        {
            lock (_lock)
            {
                if (_verifiedUsers.ContainsKey(steamId))
                {
                    _verifiedUsers.Remove(steamId);

                    SaveVerifiedUsers(); // NEW: persist removal

                    LoggerUtil.LogInfo(
                        "[VERIFY] Verification removed. SteamId="
                            + steamId
                            + " Reason="
                            + reason
                    ); // NEW

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Checks if a Steam ID is verified.
        /// <param name="steamId"></param>
        /// </summary>
        public bool IsVerified(long steamId)
        {
            lock (_lock)
            {
                return _verifiedUsers.ContainsKey(steamId);
            }
        }

        /// <summary>
        /// Gets the Discord ID for a verified Steam ID.
        /// <param name="steamId"></param>
        /// </summary>
        public ulong? GetDiscordId(long steamId)
        {
            lock (_lock)
            {
                if (_verifiedUsers.ContainsKey(steamId))
                    return _verifiedUsers[steamId];

                return null;
            }
        }

        /// <summary>
        /// Gets the number of verified users.
        /// </summary>
        public int GetVerifiedUserCount()
        {
            lock (_lock)
            {
                return _verifiedUsers.Count;
            }
        }

        
        // NEW: load verified users from XML
        private void LoadVerifiedUsers()
        {
            try
            {
                if (!File.Exists(_verifiedFilePath))
                {
                    _verifiedUsers = new Dictionary<long, ulong>();
                    return;
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(List<VerifiedUserEntry>));

                using (FileStream fs = new FileStream(_verifiedFilePath, FileMode.Open))
                {
                    List<VerifiedUserEntry> entries =
                        (List<VerifiedUserEntry>)serializer.Deserialize(fs);

                    _verifiedUsers = entries.ToDictionary(e => e.SteamId, e => e.DiscordId);
                }

                LoggerUtil.LogInfo(
                    "[VERIFY] Loaded " + _verifiedUsers.Count + " verified users from XML."
                );
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Failed to load verified users: " + ex.Message);
                _verifiedUsers = new Dictionary<long, ulong>();
            }
        }

        // NEW: save verified users to XML
        private void SaveVerifiedUsers()
        {
            try
            {
                List<VerifiedUserEntry> entries =
                    _verifiedUsers
                        .Select(kvp => new VerifiedUserEntry
                        {
                            SteamId = kvp.Key,
                            DiscordId = kvp.Value
                        })
                        .ToList();

                XmlSerializer serializer =
                    new XmlSerializer(typeof(List<VerifiedUserEntry>));

                using (FileStream fs = new FileStream(_verifiedFilePath, FileMode.Create))
                {
                    serializer.Serialize(fs, entries);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Failed to save verified users: " + ex.Message);
            }
        }

        // NEW: cleanup expired codes (can be called periodically)
        private void CleanupExpiredCodes()
        {
            lock (_lock)
            {
                var expiredCodes = _pendingCodes
                    .Where(kvp => kvp.Value.ExpirationUtc < DateTime.UtcNow)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (string code in expiredCodes)
                {
                    _pendingCodes.Remove(code);
                    LoggerUtil.LogDebug("[VERIFY] Expired verification code removed: " + code);
                }  
            }
        } 
    }
}
