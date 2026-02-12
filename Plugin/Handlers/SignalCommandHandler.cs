// Plugin/Handlers/SignalCommandHandler.cs

using System;
using System.Reflection;
using mamba.TorchDiscordSync.Plugin.Services;
using mamba.TorchDiscordSync.Plugin.Utils;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

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
                    ChatUtils.SendError("Invalid format. Use: signal:help or signal:strong:button");
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
                        ChatUtils.SendError("Access Denied: Admin only.");
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
                    ChatUtils.SendError($"Unknown action '{action}'. Use 'button' or 'spawn'.");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[SIGNAL] Exception in HandleSignalCommand: {ex}");
            }
        }

        private void HandleSignalHelp(string playerName)
        {
            string helpText =
                "=== Signal Commands ===\n"
                + "signal:help                  → this help\n"
                + "signal:strong:button         → trigger Strong signal button (server event)\n"
                + "signal:normal:button         → trigger Normal signal button (server event)\n"
                + "signal:strong:spawn (Admin)  → trigger Strong signal spawn (server event)\n"
                + "signal:normal:spawn (Admin)  → trigger Normal signal spawn (server event)";

            ChatUtils.SendHelpText(helpText);
            LoggerUtil.LogInfo($"[SIGNAL] Help requested by {playerName}");
        }

        private void HandleSignalButton(bool isStrong, long playerSteamID, string playerName)
        {
            string eventSubtype = isStrong
                ? "SpawnCargoShipSignal_Button_Strong"
                : "SpawnCargoShipSignal_Button_Normal";

            LoggerUtil.LogInfo(
                $"[SIGNAL] Button requested by {playerName} ({playerSteamID}) → {eventSubtype}"
            );

            ChatUtils.SendInfo($"Signal Button {(isStrong ? "Strong" : "Normal")} triggered.");

            TriggerGlobalEvent(eventSubtype);

            MyAPIGateway.Utilities.ShowNotification(
                $"Signal Button triggered ({(isStrong ? "Strong" : "Normal")})",
                4000,
                "Yellow"
            );

            LoggerUtil.LogInfo($"[SIGNAL] Global Event triggered for button: {eventSubtype}");
        }

        private void HandleSignalSpawn(bool isStrong, long playerSteamID, string playerName)
        {
            try
            {
                LoggerUtil.LogInfo(
                    $"[SIGNAL] Spawn requested by {playerName} ({playerSteamID}) - Strong: {isStrong}"
                );

                MyPlayer player = null;
                MySession.Static.Players.TryGetPlayerBySteamId((ulong)playerSteamID, out player);

                if (player == null || player.Identity == null)
                {
                    LoggerUtil.LogError("[SIGNAL] Spawn failed - player not found.");
                    _discordService.SendDirectMessage(playerSteamID, "ERROR: Player not found.");
                    ChatUtils.SendError("Player not found or no character.");
                    return;
                }

                string eventSubtype = isStrong
                    ? "SpawnCargoShipSignal_Strong"
                    : "SpawnCargoShipSignal_Normal";

                LoggerUtil.LogInfo($"[SIGNAL] Triggering spawn event: {eventSubtype}");

                ChatUtils.SendInfo($"Triggering Signal Spawn {(isStrong ? "Strong" : "Normal")}.");

                TriggerGlobalEvent(eventSubtype);

                MyAPIGateway.Utilities.ShowNotification(
                    $"Signal Spawn Event triggered ({(isStrong ? "Strong" : "Normal")})",
                    5000,
                    "Blue"
                );

                LoggerUtil.LogInfo($"[SIGNAL] Global Event triggered for spawn: {eventSubtype}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[SIGNAL] Exception in HandleSignalSpawn: {ex}");
            }
        }

        /// <summary>
        /// Triggers a Global Event using reflection (Torch-safe).
        /// </summary>
        private void TriggerGlobalEvent(string eventSubtype)
        {
            MySandboxGame.Static.Invoke(
                () =>
                {
                    try
                    {
                        LoggerUtil.LogInfo(
                            $"[SIGNAL] Triggering Global Event via reflection: {eventSubtype}"
                        );

                        var gameAssembly = typeof(MySandboxGame).Assembly;

                        // Internal type
                        var eventSystemType = gameAssembly.GetType(
                            "Sandbox.Game.World.MyGlobalEventSystem"
                        );

                        if (eventSystemType == null)
                        {
                            LoggerUtil.LogError("[SIGNAL] MyGlobalEventSystem type not found.");
                            return;
                        }

                        // Static instance
                        var staticProp = eventSystemType.GetProperty(
                            "Static",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                        );

                        var eventSystemInstance = staticProp?.GetValue(null);
                        if (eventSystemInstance == null)
                        {
                            LoggerUtil.LogError("[SIGNAL] MyGlobalEventSystem.Static is null.");
                            return;
                        }

                        // AddEvent method
                        var addEventMethod = eventSystemType.GetMethod(
                            "AddEvent",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        );

                        if (addEventMethod == null)
                        {
                            LoggerUtil.LogError("[SIGNAL] AddEvent method not found.");
                            return;
                        }

                        // Internal enum
                        var enumType = gameAssembly.GetType(
                            "Sandbox.Game.World.MyGlobalEventTypeEnum"
                        );

                        if (enumType == null)
                        {
                            LoggerUtil.LogError("[SIGNAL] MyGlobalEventTypeEnum not found.");
                            return;
                        }

                        var normalEnumValue = Enum.Parse(enumType, "Normal");

                        object[] parameters = { eventSubtype, normalEnumValue, 0L, null };

                        addEventMethod.Invoke(eventSystemInstance, parameters);

                        LoggerUtil.LogInfo(
                            $"[SIGNAL] Global Event triggered successfully: {eventSubtype}"
                        );
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            $"[SIGNAL] Reflection Global Event trigger failed: {ex}"
                        );
                    }
                },
                "SignalCommandHandler.TriggerGlobalEvent"
            );
        }
    }
}
