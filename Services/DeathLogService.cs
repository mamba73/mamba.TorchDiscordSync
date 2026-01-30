// Services/DeathLogService.cs
// FINAL VERSION - Works with extended DatabaseService signature

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for logging and analyzing player deaths
    /// Compatible with extended DatabaseService.LogDeath signature
    /// </summary>
    public class DeathLogService
    {
        private readonly DatabaseService _db;
        private readonly EventLoggingService _eventLog;
        private readonly DeathMessagesConfig _deathMessages;
        
        // Death history tracking
        private Dictionary<string, List<DeathHistoryModel>> _playerDeathHistory = 
            new Dictionary<string, List<DeathHistoryModel>>();

        public DeathLogService(DatabaseService db, EventLoggingService eventLog)
        {
            _db = db;
            _eventLog = eventLog;
            _deathMessages = DeathMessagesConfig.Load();
            
            LoggerUtil.LogDebug("DeathLogService initialized");
        }

        /// <summary>
        /// Main entry point: Log a player death
        /// Calls the extended DatabaseService signature with all parameters
        /// </summary>
        public async Task LogPlayerDeathAsync(
            string killerName,
            string victimName,
            string weaponType,
            long killerId,
            long victimId,
            string location)
        {
            try
            {
                LoggerUtil.LogInfo($"[DEATH] {killerName} → {victimName} ({weaponType})");

                // Ensure victim exists in history
                if (!_playerDeathHistory.ContainsKey(victimName))
                {
                    _playerDeathHistory[victimName] = new List<DeathHistoryModel>();
                }

                // Determine death type
                DeathTypeEnum deathType = DetermineDeathType(killerName, victimName);

                // Log to database with all parameters
                if (_db != null)
                {
                    try
                    {
                        // Use extended signature: LogDeath(killer, victim, deathType, weapon, location)
                        _db.LogDeath(killerId, victimId, deathType.ToString(), weaponType, location);
                        LoggerUtil.LogDebug("Death logged to database");
                    }
                    catch (Exception dbEx)
                    {
                        LoggerUtil.LogWarning($"Database logging failed: {dbEx.Message}");
                        LoggerUtil.LogDebug($"Ensure DatabaseService.LogDeath has signature: LogDeath(long, long, string, string, string)");
                    }
                }

                // Generate Discord message
                string discordMessage = GenerateDeathMessage(killerName, victimName, weaponType, deathType, location);

                // Log to Discord
                if (_eventLog != null)
                {
                    await _eventLog.LogDeathAsync(discordMessage);
                }

                // Add to local history
                try
                {
                    var deathRecord = new DeathHistoryModel();
                    SetPropertyIfExists(deathRecord, "VictimName", victimName);
                    SetPropertyIfExists(deathRecord, "VictimSteamId", victimId.ToString());
                    SetPropertyIfExists(deathRecord, "KillerName", killerName);
                    SetPropertyIfExists(deathRecord, "KillerSteamId", killerId.ToString());
                    SetPropertyIfExists(deathRecord, "Weapon", weaponType);
                    SetPropertyIfExists(deathRecord, "WeaponType", weaponType);
                    SetPropertyIfExists(deathRecord, "Location", location);
                    SetPropertyIfExists(deathRecord, "Timestamp", DateTime.UtcNow);
                    SetPropertyIfExists(deathRecord, "DeathType", deathType.ToString());

                    _playerDeathHistory[victimName].Add(deathRecord);
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogDebug($"Error adding to local history: {ex.Message}");
                }

                LoggerUtil.LogSuccess($"Death logged: {victimName} ({deathType})");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error logging death: {ex.Message}");
            }
        }

        /// <summary>
        /// Set property value by name (safe reflection)
        /// </summary>
        private void SetPropertyIfExists(object obj, string propertyName, object value)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value);
                }
            }
            catch
            {
                // Silently ignore if property doesn't exist
            }
        }

        /// <summary>
        /// Determine death type based on killer/victim history
        /// </summary>
        private DeathTypeEnum DetermineDeathType(string killerName, string victimName)
        {
            try
            {
                // Suicide
                if (string.Equals(killerName, victimName, StringComparison.OrdinalIgnoreCase))
                    return DeathTypeEnum.Suicide;

                // First kill
                if (!HasPlayerEverKilledPlayer(victimName, killerName))
                    return DeathTypeEnum.FirstKill;

                // Retaliation (< 1 hour)
                DateTime lastKillTime = GetLastKillTime(victimName, killerName);
                if (lastKillTime > DateTime.UtcNow.AddHours(-1))
                    return DeathTypeEnum.Retaliation;

                // Old retaliation (< 24 hours)
                if (lastKillTime > DateTime.UtcNow.AddHours(-24))
                    return DeathTypeEnum.RetaliationOld;

                // Accident
                return DeathTypeEnum.Accident;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error determining death type: {ex.Message}");
                return DeathTypeEnum.Accident;
            }
        }

        /// <summary>
        /// Check if victim ever killed killer
        /// </summary>
        private bool HasPlayerEverKilledPlayer(string victim, string killer)
        {
            try
            {
                if (!_playerDeathHistory.ContainsKey(killer))
                    return false;

                var killerDeaths = _playerDeathHistory[killer];
                
                return killerDeaths.Any(d => 
                {
                    try
                    {
                        var killerProp = d.GetType().GetProperty("KillerName");
                        if (killerProp?.GetValue(d) is string killerVal)
                        {
                            return string.Equals(killerVal, victim, StringComparison.OrdinalIgnoreCase);
                        }
                        return false;
                    }
                    catch { return false; }
                });
            }
            catch { return false; }
        }

        /// <summary>
        /// Get last time victim killed killer
        /// </summary>
        private DateTime GetLastKillTime(string victim, string killer)
        {
            try
            {
                if (!_playerDeathHistory.ContainsKey(killer))
                    return DateTime.MinValue;

                var lastKill = _playerDeathHistory[killer]
                    .Where(d => 
                    {
                        try
                        {
                            var killerProp = d.GetType().GetProperty("KillerName");
                            if (killerProp?.GetValue(d) is string killerVal)
                                return string.Equals(killerVal, victim, StringComparison.OrdinalIgnoreCase);
                            return false;
                        }
                        catch { return false; }
                    })
                    .OrderByDescending(d =>
                    {
                        try
                        {
                            var tsProp = d.GetType().GetProperty("Timestamp");
                            if (tsProp?.GetValue(d) is DateTime ts)
                                return ts;
                            return DateTime.MinValue;
                        }
                        catch { return DateTime.MinValue; }
                    })
                    .FirstOrDefault();

                if (lastKill == null) return DateTime.MinValue;

                var tsProp2 = lastKill.GetType().GetProperty("Timestamp");
                if (tsProp2?.GetValue(lastKill) is DateTime ts2)
                    return ts2;

                return DateTime.MinValue;
            }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// Generate Discord message from template
        /// </summary>
        private string GenerateDeathMessage(
            string killerName,
            string victimName,
            string weaponType,
            DeathTypeEnum deathType,
            string location)
        {
            try
            {
                if (_deathMessages == null)
                    return $"{killerName} killed {victimName}";

                string template = "";

                switch (deathType)
                {
                    case DeathTypeEnum.Suicide:
                        template = _deathMessages.GetRandomMessage("Suicide");
                        return string.Format(template, victimName);

                    case DeathTypeEnum.FirstKill:
                        template = _deathMessages.GetRandomMessage("FirstKill");
                        return string.Format(template, killerName, victimName, weaponType);

                    case DeathTypeEnum.Retaliation:
                        template = _deathMessages.GetRandomMessage("Retaliate");
                        return string.Format(template, killerName, victimName);

                    case DeathTypeEnum.RetaliationOld:
                        template = _deathMessages.GetRandomMessage("RetaliateOld");
                        return string.Format(template, killerName, victimName);

                    case DeathTypeEnum.Accident:
                        template = _deathMessages.GetRandomMessage("Accident");
                        return string.Format(template, victimName);

                    default:
                        return $"{killerName} killed {victimName}";
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error generating message: {ex.Message}");
                return $"{killerName} killed {victimName}";
            }
        }

        /// <summary>
        /// Get player death history
        /// </summary>
        public List<DeathHistoryModel> GetPlayerDeaths(string playerName)
        {
            try
            {
                if (_playerDeathHistory.TryGetValue(playerName, out var deaths))
                    return new List<DeathHistoryModel>(deaths);
                return new List<DeathHistoryModel>();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error getting deaths: {ex.Message}");
                return new List<DeathHistoryModel>();
            }
        }

        /// <summary>
        /// Get player stats (K/D ratio)
        /// </summary>
        public (int Deaths, int Kills, float KDRatio) GetPlayerStats(string playerName)
        {
            try
            {
                int deaths = 0;
                int kills = 0;

                if (_playerDeathHistory.TryGetValue(playerName, out var playerDeaths))
                {
                    deaths = playerDeaths.Count;

                    foreach (var deathList in _playerDeathHistory.Values)
                    {
                        kills += deathList.Count(d =>
                        {
                            try
                            {
                                var killerProp = d.GetType().GetProperty("KillerName");
                                if (killerProp?.GetValue(d) is string killerVal)
                                    return string.Equals(killerVal, playerName, StringComparison.OrdinalIgnoreCase);
                                return false;
                            }
                            catch { return false; }
                        });
                    }
                }

                float kd = deaths > 0 ? (float)kills / deaths : kills;
                return (deaths, kills, kd);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error getting stats: {ex.Message}");
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// Get top killers leaderboard
        /// </summary>
        public List<(string PlayerName, int Kills)> GetTopKillers(int limit = 10)
        {
            try
            {
                var killerStats = new Dictionary<string, int>();

                foreach (var deathList in _playerDeathHistory.Values)
                {
                    foreach (var death in deathList)
                    {
                        try
                        {
                            var killerProp = death.GetType().GetProperty("KillerName");
                            if (killerProp?.GetValue(death) is string killerName && !string.IsNullOrEmpty(killerName))
                            {
                                if (!killerStats.ContainsKey(killerName))
                                    killerStats[killerName] = 0;
                                killerStats[killerName]++;
                            }
                        }
                        catch { }
                    }
                }

                return killerStats
                    .OrderByDescending(x => x.Value)
                    .Take(limit)
                    .Select(x => (x.Key, x.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogDebug($"Error getting leaderboard: {ex.Message}");
                return new List<(string, int)>();
            }
        }

        /// <summary>
        /// Clear history
        /// </summary>
        public void ClearHistory()
        {
            _playerDeathHistory.Clear();
            LoggerUtil.LogInfo("Death history cleared");
        }
    }
}