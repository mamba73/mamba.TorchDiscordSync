// Plugin/Services/VerificationService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Plugin.Utils;

namespace mamba.TorchDiscordSync.Plugin.Services
{
    public class VerificationService
    {
        private readonly DatabaseService _db;
        private readonly int _verificationCodeExpirationMinutes;
        private const int CodeLength = 8;
        private static readonly Random _random = new Random();

        public VerificationService(DatabaseService db, int verificationCodeExpirationMinutes = 15)
        {
            _db = db;
            _verificationCodeExpirationMinutes = verificationCodeExpirationMinutes;
            LoggerUtil.LogInfo(
                $"[VERIFY_SERVICE] Initialized with {verificationCodeExpirationMinutes} minute expiration"
            );
        }

        /// <summary>
        /// Generate a new verification code for a player
        /// KORISTI: _db.AddPendingVerification() → VerificationPlayers.xml
        /// </summary>
        public string GenerateVerificationCode(
            long steamID,
            string playerName,
            string discordUsername,
            string gamePlayerName = null
        )
        {
            try
            {
                LoggerUtil.LogDebug(
                    $"[VERIFY_SERVICE] GenerateVerificationCode for {playerName} (SteamID: {steamID})"
                );

                // Check if player already has pending verification
                var existing = _db.GetPendingVerification(steamID);
                if (existing != null && existing.ExpiresAt > DateTime.UtcNow)
                {
                    LoggerUtil.LogWarning($"[VERIFY] {playerName}: Already has pending code");
                    return null; // Code still valid, don't generate new one
                }

                // Generate random code
                string code = GenerateRandomCode(CodeLength);

                // Use provided gamePlayerName or fallback to playerName
                string nameToStore = !string.IsNullOrEmpty(gamePlayerName)
                    ? gamePlayerName
                    : playerName;

                // NOVO: Save to VerificationPlayers.xml (NE u staru bazu!)
                _db.AddPendingVerification(
                    steamID,
                    discordUsername,
                    code,
                    _verificationCodeExpirationMinutes,
                    nameToStore
                );

                LoggerUtil.LogSuccess($"[VERIFY] Generated code for {playerName}: {code}");
                LoggerUtil.LogDebug(
                    $"[VERIFY] Saved to VerificationPlayers.xml (expires in {_verificationCodeExpirationMinutes} min)"
                );

                return code;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY] Code generation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verify the code from Discord bot
        /// KORISTI: _db.MarkAsVerified() → VerificationPlayers.xml
        /// </summary>
        public Task<string> VerifyAsync(string code, ulong discordId, string discordUsername)
        {
            try
            {
                LoggerUtil.LogDebug($"[VERIFY_SERVICE] VerifyAsync called with code: {code}");

                if (string.IsNullOrEmpty(code))
                {
                    LoggerUtil.LogWarning("[VERIFY] Empty verification code");
                    return Task.FromResult("Invalid verification code");
                }

                // Find verification by code iz VerificationPlayers.xml
                var allPending = _db.GetAllPendingVerifications();
                var verification = allPending.Find(p => p.VerificationCode == code);

                if (verification == null)
                {
                    LoggerUtil.LogWarning($"[VERIFY] Code not found: {code}");
                    return Task.FromResult("Verification code not found!");
                }

                // Check if code is expired
                if (verification.ExpiresAt < DateTime.UtcNow)
                {
                    LoggerUtil.LogWarning($"[VERIFY] Code expired: {code}");
                    _db.DeletePendingVerification(verification.SteamID);
                    return Task.FromResult("Verification code has expired!");
                }

                // NOVO: Mark as verified u VerificationPlayers.xml
                _db.MarkAsVerified(verification.SteamID, discordUsername, discordId);

                LoggerUtil.LogSuccess(
                    $"[VERIFY] Verified: SteamID {verification.SteamID} → Discord {discordUsername} (ID: {discordId})"
                );
                LoggerUtil.LogDebug(
                    $"[VERIFY] Saved to VerificationPlayers.xml as verified player"
                );

                return Task.FromResult("Verification successful!");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY] Verification failed: {ex.Message}");
                return Task.FromResult($"Verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if player je već verificiran
        /// </summary>
        public bool IsPlayerVerified(long steamID)
        {
            try
            {
                var verified = _db.GetVerifiedPlayer(steamID);
                return verified != null;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY] Error checking verification status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove verification (admin command)
        /// </summary>
        public bool RemoveVerification(long steamID, string reason = "Admin removal")
        {
            try
            {
                _db.DeletePendingVerification(steamID);
                _db.DeleteVerifiedPlayer(steamID);
                LoggerUtil.LogInfo(
                    $"[VERIFY] Removed verification for SteamID {steamID}: {reason}"
                );
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[VERIFY] Remove verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate random code sa slučajnim znakovima
        /// </summary>
        private string GenerateRandomCode(int length)
        {
            // const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var result = "";
            for (int i = 0; i < length; i++)
            {
                result += chars[_random.Next(chars.Length)];
            }
            return result;
        }
    }
}
