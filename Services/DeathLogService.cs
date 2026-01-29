// Services/DeathLogService.cs
using System;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Services
{
    public class DeathLogService
    {
        private readonly DatabaseService _db;
        private readonly EventLoggingService _eventLog;
        private readonly DeathMessagesConfig _deathMessages;
        private const int RetaliateWindow = 3600;
        private const int RetaliateOldWindow = 86400;

        public DeathLogService(DatabaseService db, EventLoggingService eventLog)
        {
            _db = db;
            _eventLog = eventLog;
            _deathMessages = DeathMessagesConfig.Load();
        }

        public async Task LogPlayerDeathAsync(string killerName, string victimName,
            string weapon, long killerSteamID, long victimSteamID, string location)
        {
            if (string.IsNullOrEmpty(location))
                location = "Unknown";

            try
            {
                string deathType = "Accident";
                string message = "";

                if (killerSteamID == victimSteamID)
                {
                    deathType = "Suicide";
                    message = victimName + " committed suicide";
                }
                else if (killerSteamID > 0)
                {
                    var lastKill = _db.GetLastKill(killerSteamID, victimSteamID);

                    if (lastKill != null)
                    {
                        var timeSinceLastKill = (DateTime.UtcNow - lastKill.DeathTime).TotalSeconds;

                        if (timeSinceLastKill < RetaliateWindow)
                        {
                            deathType = "Retaliate";
                            message = killerName + " retaliated against " + victimName;
                        }
                        else if (timeSinceLastKill < RetaliateOldWindow)
                        {
                            deathType = "RetaliateOld";
                            message = killerName + " took revenge on " + victimName;
                        }
                        else
                        {
                            deathType = "FirstKill";
                            message = killerName + " killed " + victimName + " with " + weapon + " at " + location;
                        }
                    }
                    else
                    {
                        deathType = "FirstKill";
                        message = killerName + " killed " + victimName + " with " + weapon + " at " + location;
                    }
                }
                else
                {
                    deathType = "Accident";
                    message = victimName + " died at " + location;
                }

                _db.LogDeath(killerSteamID, victimSteamID, deathType);

                if (_eventLog != null)
                {
                    await _eventLog.LogDeathAsync(message);
                }

                LoggerUtil.LogInfo("[DEATH] " + message);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Death log error: " + ex.Message);
            }
        }
    }
}