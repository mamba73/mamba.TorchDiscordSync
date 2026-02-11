using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Plugin.Utils;

namespace mamba.TorchDiscordSync.Plugin.Services
{
    public class VerificationService
    {
        private readonly object _lock = new object();

        // existing dependencies (do NOT change constructor signature)
        private readonly object _storage;   // whatever you already pass in index.cs
        private readonly object _config;    // whatever you already pass in index.cs

        // NEW: pending verification codes in memory
        private readonly Dictionary<string, PendingVerification> _pendingCodes =
            new Dictionary<string, PendingVerification>();

        // NEW: verified users cache (SteamId -> DiscordId)
        private readonly Dictionary<long, ulong> _verifiedUsers =
            new Dictionary<long, ulong>();

        // DO NOT CHANGE – constructor must match index.cs
        public VerificationService(object storage, object config)
        {
            _storage = storage;
            _config = config;
        }

        private class PendingVerification
        {
            public long SteamId;
            public string PlayerName;
            public string DiscordUsername;
            public DateTime ExpirationUtc;
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
                    "[VERIFY] Pending verification created. SteamId="
                        + steamId
                        + " Code="
                        + code
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

                // NEW: remove pending entry
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

        // NEW: helper for other systems (roles, sync, etc.)
        public bool IsVerified(long steamId)
        {
            lock (_lock)
            {
                return _verifiedUsers.ContainsKey(steamId);
            }
        }

        // NEW: helper to get DiscordId for SteamId
        public ulong? GetDiscordId(long steamId)
        {
            lock (_lock)
            {
                if (_verifiedUsers.ContainsKey(steamId))
                    return _verifiedUsers[steamId];

                return null;
            }
        }
    }
}
