// Plugin/Handlers/SignalCommandHandler.cs

using System;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace mamba.TorchDiscordSync.Plugin.Handlers
{
    public class SignalCommandHandler
    {
        private readonly DiscordService _discordService;

        public SignalCommandHandler(DiscordService discordService)
        {
            _discordService = discordService;
        }

        public void HandleSignalCommand(
            string subcommand,
            long playerSteamID,
            string playerName,
            bool isAdmin
        )
        {
            try
            {
                var parts = subcommand.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                // === SIGNAL:HELP ===
                if (parts.Length == 2 && parts[1].ToLower() == "help")
                {
                    HandleSignalHelp(playerName);
                    return;
                }

                if (parts.Length < 3)
                {
                    _discordService.SendDirectMessage(
                        playerSteamID,
                        "Invalid format. Use: signal:help or signal:strong:button"
                    );
                    return;
                }

                string strengthStr = parts[1].ToLower();
                string action = parts[2].ToLower();
                bool isStrong = strengthStr == "strong";

                if (action == "button")
                {
                    HandleSignalButton(isStrong, playerSteamID, playerName);
                }
                else if (action == "spawn")
                {
                    if (!isAdmin)
                    {
                        _discordService.SendDirectMessage(
                            playerSteamID,
                            "Access Denied: Admin only."
                        );
                        return;
                    }
                    HandleSignalSpawn(isStrong, playerSteamID, playerName);
                }
                else
                {
                    _discordService.SendDirectMessage(
                        playerSteamID,
                        $"Unknown action '{action}'. Use 'button' or 'spawn'."
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[SIGNAL] Exception in HandleSignalCommand: {ex.Message}");
            }
        }

        private void HandleSignalHelp(string playerName)
        {
            string helpText =
                "=== Signal Commands ===\n"
                + "signal:help                  → this help\n"
                + "signal:strong:button         → simulate Strong signal button click\n"
                + "signal:normal:button         → simulate Normal signal button click\n"
                + "signal:strong:spawn (Admin)  → spawn Strong signal 10m in front\n"
                + "signal:normal:spawn (Admin)  → spawn Normal signal 10m in front";

            MyAPIGateway.Utilities.ShowNotification(helpText, 10000, "White");

            LoggerUtil.LogInfo($"User {playerName} requested Signal help");
        }

        private void HandleSignalButton(bool isStrong, long playerSteamID, string playerName)
        {
            string strength = isStrong ? "Strong" : "Normal";
            string color = isStrong ? "Red" : "Yellow";

            MyAPIGateway.Utilities.ShowNotification(
                $"{strength} Signal Button clicked!",
                3000,
                color
            );

            LoggerUtil.LogInfo($"User {playerName} triggered Signal Button: {strength}");
        }

        private void HandleSignalSpawn(bool isStrong, long playerSteamID, string playerName)
        {
            MyPlayer player = null;
            MySession.Static.Players.TryGetPlayerBySteamId((ulong)playerSteamID, out player);

            if (player == null || player.Identity == null)
            {
                _discordService.SendDirectMessage(
                    playerSteamID,
                    "ERROR: Player not found or no character."
                );
                return;
            }

            if (player.Controller.ControlledEntity is IMyShipController)
            {
                _discordService.SendDirectMessage(playerSteamID, "ERROR: Exit cockpit first!");
                return;
            }

            MyAPIGateway.Utilities.ShowNotification(
                $"Spawned {(isStrong ? "Strong" : "Normal")} Signal 10m ahead.",
                5000,
                "Blue"
            );

            LoggerUtil.LogInfo(
                $"Admin {playerName} spawned {(isStrong ? "Strong" : "Normal")} Signal (10m ahead)"
            );
        }
    }
}
