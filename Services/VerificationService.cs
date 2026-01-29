// Services/VerificationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    public class VerificationService
    {
        private readonly DatabaseService _db;
        private const int VerificationCodeExpirationMinutes = 15;
        private const int CodeLength = 8;
        private static readonly Random _random = new Random();

        public VerificationService(DatabaseService db)
        {
            _db = db;
        }

        public Task<string> VerifyAsync(string code, ulong discordId, string discordUsername)
        {
            bool result = VerifyCode(code, discordId, discordUsername);
            string message = result ? "Verification successful!" : "Verification failed!";
            return Task.FromResult(message);
        }

        /// <summary>
        /// Generate a new verification code for a player
        /// Returns the code to display to player
        /// </summary>
        public string GenerateVerificationCode(long steamID, string playerName, string discordUsername)
        {
            try
            {
                // Check if player already has pending verification
                var existing = _db.GetVerification(steamID);
                if (existing != null && !existing.IsVerified && !IsCodeExpired(existing))
                {
                    LoggerUtil.LogWarning("[VERIFY] " + playerName + ": Already has pending code");
                    return null; // Code still valid, don't generate new one
                }

                // Generate random code
                string code = GenerateRandomCode(CodeLength);

                // Create verification model
                var verification = new VerificationModel
                {
                    SteamID = steamID,
                    VerificationCode = code,
                    CodeGeneratedAt = DateTime.UtcNow,
                    DiscordUsername = discordUsername,
                    IsVerified = false
                };

                // Save to database
                _db.SaveVerification(verification);

                LoggerUtil.LogInfo("[VERIFY] Generated code for " + playerName + ": " + code);
                return code;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Code generation failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Verify the code from Discord bot
        /// Should be called when Discord bot receives !verify code command
        /// </summary>
        public bool VerifyCode(string code, ulong discordUserID, string discordUsername)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                {
                    LoggerUtil.LogWarning("[VERIFY] Empty verification code");
                    return false;
                }

                // Find verification by code
                var verification = _db.GetVerificationByCode(code);
                if (verification == null)
                {
                    LoggerUtil.LogWarning("[VERIFY] Code not found: " + code);
                    return false;
                }

                // Check if code is expired
                if (IsCodeExpired(verification))
                {
                    LoggerUtil.LogWarning("[VERIFY] Code expired: " + code);
                    _db.DeleteVerification(verification.SteamID);
                    return false;
                }

                // Check if already verified
                if (verification.IsVerified)
                {
                    LoggerUtil.LogWarning("[VERIFY] Code already used: " + code);
                    return false;
                }

                // Mark as verified
                verification.IsVerified = true;
                verification.VerifiedAt = DateTime.UtcNow;
                verification.DiscordUserID = discordUserID;
                verification.DiscordUsername = discordUsername;

                _db.SaveVerification(verification);

                // Log to history
                var historyEntry = new VerificationHistoryModel
                {
                    SteamID = verification.SteamID,
                    DiscordUsername = discordUsername,
                    DiscordUserID = discordUserID,
                    VerifiedAt = DateTime.UtcNow,
                    Status = "Success"
                };
                _db.SaveVerificationHistory(historyEntry);

                LoggerUtil.LogSuccess("[VERIFY] Verified: SteamID " + verification.SteamID + " -> Discord " + discordUsername + " (" + discordUserID + ")");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Verification failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Get Discord User ID for a given Steam ID
        /// Returns 0 if not verified
        /// </summary>
        public ulong GetDiscordUserID(long steamID)
        {
            try
            {
                var verification = _db.GetVerification(steamID);
                if (verification != null && verification.IsVerified)
                    return verification.DiscordUserID;

                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Get Discord ID failed: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get Discord username for a given Steam ID
        /// </summary>
        public string GetDiscordUsername(long steamID)
        {
            try
            {
                var verification = _db.GetVerification(steamID);
                if (verification != null && verification.IsVerified)
                    return verification.DiscordUsername;

                return null;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Get Discord username failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Check if a Steam ID is verified
        /// </summary>
        public bool IsVerified(long steamID)
        {
            try
            {
                var verification = _db.GetVerification(steamID);
                return verification != null && verification.IsVerified;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] IsVerified check failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Remove verification for a player (admin command)
        /// </summary>
        public bool RemoveVerification(long steamID, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "Admin removal";

            try
            {
                var verification = _db.GetVerification(steamID);
                if (verification == null)
                    return false;

                var historyEntry = new VerificationHistoryModel
                {
                    SteamID = steamID,
                    DiscordUsername = verification.DiscordUsername,
                    DiscordUserID = verification.DiscordUserID,
                    VerifiedAt = DateTime.UtcNow,
                    Status = "Removed"
                };
                _db.SaveVerificationHistory(historyEntry);

                _db.DeleteVerification(steamID);
                LoggerUtil.LogInfo("[VERIFY] Removed verification for SteamID " + steamID + ": " + reason);
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Remove verification failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Clean up expired verification codes
        /// Should be called periodically
        /// </summary>
        public void CleanupExpiredCodes()
        {
            try
            {
                var verifications = _db.GetAllVerifications();
                int removedCount = 0;

                for (int i = 0; i < verifications.Count; i++)
                {
                    if (!verifications[i].IsVerified && IsCodeExpired(verifications[i]))
                    {
                        _db.DeleteVerification(verifications[i].SteamID);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                    LoggerUtil.LogInfo("[VERIFY] Cleaned up " + removedCount + " expired verification codes");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[VERIFY] Cleanup failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Private helper: Check if verification code is expired
        /// </summary>
        private bool IsCodeExpired(VerificationModel verification)
        {
            var age = DateTime.UtcNow - verification.CodeGeneratedAt;
            return age.TotalMinutes > VerificationCodeExpirationMinutes;
        }

        /// <summary>
        /// Private helper: Generate random alphanumeric code
        /// </summary>
        private string GenerateRandomCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[_random.Next(chars.Length)];
            }
            return new string(result);
        }
    }
}