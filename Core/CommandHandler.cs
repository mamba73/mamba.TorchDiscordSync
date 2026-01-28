// Core/CommandHandler.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Services;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Core
{
    public class CommandHandler
    {
        private readonly DatabaseService _db;
        private readonly EventLoggingService _eventLog;
        private readonly SyncOrchestrator _syncService;
        private readonly MainConfig _config;

        public CommandHandler(DatabaseService db, EventLoggingService eventLog, SyncOrchestrator syncService, MainConfig config)
        {
            _db = db;
            _eventLog = eventLog;
            _syncService = syncService;
            _config = config;
        }

        private bool IsAdmin(long steamId)
        {
            long[] admins = null;
            if (_config != null && _config.AdminSteamIDs != null)
            {
                admins = _config.AdminSteamIDs;
            }
            else
            {
                admins = new long[0];
            }
            
            if (admins.Length > 0)
            {
                for (int i = 0; i < admins.Length; i++)
                {
                    if (admins[i] == steamId)
                        return true;
                }
            }
            return false;
        }

        public async Task<bool> ExecuteCommandAsync(string command, long playerSteamID,
            string playerName)
        {
            bool isAdmin = IsAdmin(playerSteamID);
            if (!isAdmin)
            {
                LoggerUtil.LogWarning("Denied command from " + playerName + " (" + playerSteamID + ")");
                return false;
            }

            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return false;

            var cmd = parts[0].ToLower();

            try
            {
                switch (cmd)
                {
                    case "/tds":
                    case "tds":
                        if (parts.Length < 2)
                            return false;

                        var subcommand = parts[1].ToLower();

                        switch (subcommand)
                        {
                            case "sync":
                                await HandleSyncCommand(playerName);
                                return true;

                            case "reset":
                                await HandleResetCommand(playerName);
                                return true;

                            case "status":
                                await HandleStatusCommand(playerName);
                                return true;

                            default:
                                return false;
                        }

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Command execution failed: " + ex.Message);
                if (_eventLog != null)
                {
                    await _eventLog.LogAsync("CommandError", playerName + ": " + ex.Message);
                }
                return false;
            }
        }

        private async Task HandleSyncCommand(string playerName)
        {
            LoggerUtil.LogInfo(playerName + " initiated manual sync");
            if (_eventLog != null)
            {
                await _eventLog.LogAsync("Command", "Manual sync triggered by " + playerName);
            }
            Console.WriteLine("[SYNC] Manual sync command queued");
        }

        private async Task HandleResetCommand(string playerName)
        {
            LoggerUtil.LogInfo(playerName + " initiated Discord reset");

            if (_eventLog != null)
            {
                await _eventLog.LogAsync("Command", "Discord reset executed by " + playerName);
            }

            LoggerUtil.LogInfo("Reset completed by " + playerName);
        }

        private async Task HandleStatusCommand(string playerName)
        {
            var factions = _db.GetAllFactions();
            int totalPlayers = 0;
            
            if (factions != null)
            {
                for (int i = 0; i < factions.Count; i++)
                {
                    if (factions[i].Players != null)
                        totalPlayers += factions[i].Players.Count;
                }
            }

            string status = "Plugin Status - Factions: " + (factions != null ? factions.Count : 0) + 
                           ", Players: " + totalPlayers + 
                           ", Debug: " + (_config != null ? _config.Debug : false);

            LoggerUtil.LogInfo(status);
            if (_eventLog != null)
            {
                await _eventLog.LogAsync("Status", status);
            }
        }

        public Task<bool> HandleCommandAsync(string cmd, long steamId)
        {
            bool isAdmin = IsAdmin(steamId);
            if (!isAdmin)
            {
                LoggerUtil.LogWarning("Unauthorized command: " + cmd + " by " + steamId);
                if (_eventLog != null)
                {
                    Task.Run(delegate
                    {
                        return _eventLog.LogAsync("UnauthorizedCommand", cmd + " by " + steamId);
                    });
                }
                return Task.FromResult(false);
            }

            LoggerUtil.LogInfo("Command executed: " + cmd + " by " + steamId);
            if (_eventLog != null)
            {
                Task.Run(delegate
                {
                    return _eventLog.LogAsync("CommandExecuted", cmd + " by " + steamId);
                });
            }
            return Task.FromResult(true);
        }
    }
}