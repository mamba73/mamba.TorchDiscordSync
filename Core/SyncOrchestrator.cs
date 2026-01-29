// Core/SyncOrchestrator.cs
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Core
{
    /// <summary>
    /// Orchestrates all synchronization operations
    /// Coordinates between multiple services to ensure proper sync flow
    /// </summary>
    public class SyncOrchestrator
    {
        private readonly MainConfig _config;
        private readonly DatabaseService _db;
        private readonly DiscordService _discord;
        private readonly FactionSyncService _factionSync;
        private readonly EventLoggingService _eventLog;

        public SyncOrchestrator(DatabaseService db, DiscordService discord,
            FactionSyncService factionSync, EventLoggingService evtLog, MainConfig config)
        {
            _db = db;
            _discord = discord;
            _factionSync = factionSync;
            _eventLog = evtLog;
            _config = config;
        }

        /// <summary>
        /// Execute full synchronization of factions, players, and Discord objects
        /// </summary>
        public async Task ExecuteFullSyncAsync(List<FactionModel> factions)
        {
            try
            {
                LoggerUtil.LogInfo("[ORCHESTRATOR] Starting full synchronization");

                // 1. Synchronize database
                LoggerUtil.LogDebug("Syncing database...", _config != null && _config.Debug);
                foreach (var faction in factions)
                {
                    if (faction.Tag.Length == 3)
                    {
                        _db.SaveFaction(faction);
                    }
                }

                // 2. Synchronize Discord
                LoggerUtil.LogDebug("Syncing Discord...", _config != null && _config.Debug);
                var playerFactions = new List<FactionModel>();
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].Tag.Length == 3)
                        playerFactions.Add(factions[i]);
                }
                
                await _factionSync.SyncFactionsAsync(playerFactions);

                // 3. Log completion
                int playerCount = 0;
                for (int i = 0; i < playerFactions.Count; i++)
                {
                    if (playerFactions[i].Players != null)
                        playerCount += playerFactions[i].Players.Count;
                }
                await _eventLog.LogSyncCompleteAsync(playerFactions.Count, playerCount);

                LoggerUtil.LogInfo("[ORCHESTRATOR] Synchronization complete");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[ORCHESTRATOR] Sync error: " + ex.Message);
                if (_eventLog != null)
                {
                    await _eventLog.LogAsync("SyncError", ex.Message);
                }
            }
        }

        /// <summary>
        /// Check and report server status
        /// </summary>
        public async Task CheckServerStatusAsync(float currentSimSpeed)
        {
            try
            {
                if (_config != null && _config.Monitoring != null)
                {
                    if (currentSimSpeed < _config.Monitoring.SimThresh)
                    {
                        if (_eventLog != null)
                        {
                            await _eventLog.LogSimSpeedWarningAsync(currentSimSpeed);
                        }
                    }
                }

                if (_eventLog != null)
                {
                    await _eventLog.LogServerStatusAsync("UP", currentSimSpeed);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("[ORCHESTRATOR] Status check error: " + ex.Message);
            }
        }

        public Task SyncFactionsAsync()
        {
            if (_config != null && _config.Debug)
            {
                LoggerUtil.LogInfo("Starting faction sync...");
            }
            
            if (_factionSync != null)
            {
                return _factionSync.SyncFactionsAsync(new List<FactionModel>());
            }
            return Task.FromResult(0);
        }

        public Task LogEventAsync(string eventName, string details)
        {
            if (_eventLog != null)
            {
                return _eventLog.LogAsync(eventName, details);
            }
            return Task.FromResult(0);
        }

        public Task CheckSimSpeedAsync(float currentSimSpeed)
        {
            try
            {
                if (_config == null || _config.Monitoring == null)
                    return Task.FromResult(0);

                float threshold = _config.Monitoring.SimThresh;
                
                if (currentSimSpeed < threshold)
                {
                    LoggerUtil.LogWarning("SimSpeed below threshold: " + currentSimSpeed.ToString("F2"));
                    
                    if (_eventLog != null)
                    {
                        return _eventLog.LogSimSpeedWarningAsync(currentSimSpeed);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("SimSpeed check error: " + ex.Message);
            }

            return Task.FromResult(0);
        }
    }
}