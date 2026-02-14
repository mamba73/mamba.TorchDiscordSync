// Plugin/Handlers/TestCommandHandler.cs

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
    public class TestCommandHandler
    {
        private readonly DiscordService _discordService;

        public TestCommandHandler(DiscordService discordService)
        {
            _discordService = discordService;
        }

        public void HandleTestCommand(
            string subcommand,
            long playerSteamID,
            string playerName,
            bool isAdmin
        )
        {
            try
            {
                var parts = subcommand.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                // === TEST:HELP ===
                if (parts.Length == 2 && parts[1].ToLower() == "help")
                {
                    HandleTestHelp(playerName);
                    return;
                }

                if (parts.Length < 3)
                {
                    _discordService.SendDirectMessage(
                        playerSteamID,
                        "Invalid format. Use: test:help or test:strong:button"
                    );
                    ChatUtils.SendError("Invalid format. Use: test:help or test:strong:button");
                    return;
                }

                string strengthStr = parts[1].ToLower();
                string action = parts[2].ToLower();
                bool isStrong = strengthStr == "strong";

                if (action == "button")
                {
                    HandleTestButton(isStrong, playerSteamID, playerName);
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

                    HandleTestSpawn(isStrong, playerSteamID, playerName);
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
                LoggerUtil.LogError($"[TEST] Exception in HandleTestCommand: {ex}");
            }
        }

        private void HandleTestHelp(string playerName)
        {
            string helpText =
                "=== Test Commands ===\n"
                + "test:help                  → this help\n"
                + "test:strong:button         → trigger Strong test button (server event)\n"
                + "test:normal:button         → trigger Normal test button (server event)\n"
                + "test:strong:spawn (Admin)  → trigger Strong test spawn (server event)\n"
                + "test:normal:spawn (Admin)  → trigger Normal test spawn (server event)";

            MyAPIGateway.Utilities.ShowNotification(helpText, 10000, "White");

            LoggerUtil.LogInfo($"[TEST] Help requested by {playerName}");
        }

        private void HandleTestButton(bool isStrong, long playerSteamID, string playerName)
        {
            string eventSubtype = isStrong
                ? "SpawnCargoShipTest_Button_Strong"
                : "SpawnCargoShipTest_Button_Normal";

            LoggerUtil.LogInfo(
                $"[TEST] Button requested by {playerName} ({playerSteamID}) → {eventSubtype}"
            );

            ChatUtils.SendInfo($"Test Button {(isStrong ? "Strong" : "Normal")} triggered.");

            TriggerGlobalEvent(eventSubtype);

            MyAPIGateway.Utilities.ShowNotification(
                $"Test Button triggered ({(isStrong ? "Strong" : "Normal")})",
                4000,
                "Yellow"
            );

            LoggerUtil.LogInfo($"[TEST] Global Event triggered for button: {eventSubtype}");
        }

        private void HandleTestSpawn(bool isStrong, long playerSteamID, string playerName)
        {
            try
            {
                LoggerUtil.LogInfo(
                    $"[TEST] Spawn requested by {playerName} ({playerSteamID}) - Strong: {isStrong}"
                );

                MyPlayer player = null;
                MySession.Static.Players.TryGetPlayerBySteamId((ulong)playerSteamID, out player);

                if (player == null || player.Identity == null)
                {
                    LoggerUtil.LogError("[TEST] Spawn failed - player not found.");
                    _discordService.SendDirectMessage(playerSteamID, "ERROR: Player not found.");
                    ChatUtils.SendError("Player not found or no character.");
                    return;
                }

                string eventSubtype = isStrong
                    ? "SpawnCargoShipTest_Strong"
                    : "SpawnCargoShipTest_Normal";

                LoggerUtil.LogInfo($"[TEST] Triggering spawn event: {eventSubtype}");
                TriggerGlobalEvent(eventSubtype);

                MyAPIGateway.Utilities.ShowNotification(
                    $"Test Spawn Event triggered ({(isStrong ? "Strong" : "Normal")})",
                    5000,
                    "Blue"
                );

                LoggerUtil.LogInfo($"[TEST] Global Event triggered for spawn: {eventSubtype}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[TEST] Exception in HandleTestSpawn: {ex}");
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
                            $"[TEST] Triggering Global Event via reflection: {eventSubtype}"
                        );

                        var gameAssembly = typeof(MySandboxGame).Assembly;

                        // Internal type
                        var eventSystemType = gameAssembly.GetType(
                            "Sandbox.Game.World.MyGlobalEventSystem"
                        );

                        if (eventSystemType == null)
                        {
                            LoggerUtil.LogError("[TEST] MyGlobalEventSystem type not found.");
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
                            LoggerUtil.LogError("[TEST] MyGlobalEventSystem.Static is null.");
                            return;
                        }

                        // AddEvent method
                        var addEventMethod = eventSystemType.GetMethod(
                            "AddEvent",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        );

                        if (addEventMethod == null)
                        {
                            LoggerUtil.LogError("[TEST] AddEvent method not found.");
                            return;
                        }

                        // Internal enum
                        var enumType = gameAssembly.GetType(
                            "Sandbox.Game.World.MyGlobalEventTypeEnum"
                        );

                        if (enumType == null)
                        {
                            LoggerUtil.LogError("[TEST] MyGlobalEventTypeEnum not found.");
                            return;
                        }

                        var normalEnumValue = Enum.Parse(enumType, "Normal");

                        object[] parameters = { eventSubtype, normalEnumValue, 0L, null };

                        addEventMethod.Invoke(eventSystemInstance, parameters);

                        LoggerUtil.LogInfo(
                            $"[TEST] Global Event triggered successfully: {eventSubtype}"
                        );
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.LogError(
                            $"[TEST] Reflection Global Event trigger failed: {ex}"
                        );
                    }
                },
                "TestCommandHandler.TriggerGlobalEvent");
        }
    }
}
