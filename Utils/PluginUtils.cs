// Utils/PluginUtils.cs
using System;
using System.Timers;
using mamba.TorchDiscordSync.Config;
using mamba.TorchDiscordSync.Utils;
using Sandbox;
using Sandbox.Game;

namespace mamba.TorchDiscordSync.Utils
{
    /// <summary>
    /// Helper methods for MambaTorchDiscordSyncPlugin to reduce duplication.
    /// Contains config checks, SimSpeed retrieval, and timer management.
    /// </summary>
    public static class PluginUtils
    {
        /// <summary>
        /// Checks if faction synchronization is enabled in config.
        /// </summary>
        public static bool IsFactionSyncEnabled(MainConfig config)
        {
            return config != null && config.Faction != null && config.Faction.Enabled;
        }

        /// <summary>
        /// Checks if server monitoring is enabled in config.
        /// </summary>
        public static bool IsMonitoringEnabled(MainConfig config)
        {
            return config != null && config.Monitoring != null && config.Monitoring.Enabled;
        }

        /// <summary>
        /// Checks if chat server-to-discord sync is enabled in config.
        /// </summary>
        public static bool IsChatServerToDiscordEnabled(MainConfig config)
        {
            return config != null && config.Chat != null && config.Chat.ServerToDiscord;
        }

        /// <summary>
        /// Gets the current simulation speed (SimSpeed) in a safe way.
        /// Returns 1.0f if unable to retrieve.
        /// </summary>
        public static float GetCurrentSimSpeed()
        {
            try
            {
                float simSpeed = MySandboxGame.SimulationRatio;
                if (float.IsNaN(simSpeed) || float.IsInfinity(simSpeed))
                {
                    LoggerUtil.LogWarning(
                        "Invalid SimSpeed detected (NaN/Infinity) - returning default 1.0"
                    );
                    return 1.0f;
                }
                return simSpeed;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Error getting SimSpeed: " + ex.Message);
                return 1.0f;
            }
        }

        /// <summary>
        /// Creates and configures the sync timer only if faction sync is enabled and interval is valid.
        /// Returns the timer instance or null if not started.
        /// </summary>
        public static Timer CreateSyncTimerIfEnabled(
            MainConfig config,
            ElapsedEventHandler elapsedHandler
        )
        {
            if (!IsFactionSyncEnabled(config))
            {
                LoggerUtil.LogInfo(
                    "Faction sync timer NOT created - faction sync is disabled in config"
                );
                return null;
            }

            int intervalSeconds = config.SyncIntervalSeconds;
            if (intervalSeconds <= 0)
            {
                LoggerUtil.LogInfo("Faction sync timer NOT created - interval is 0 or negative");
                return null;
            }

            int intervalMs = intervalSeconds * 1000;

            Timer timer = new Timer(intervalMs);
            timer.Elapsed += elapsedHandler;
            timer.AutoReset = true;

            LoggerUtil.LogInfo(
                $"Faction sync timer created (interval: {intervalSeconds}s / {intervalMs}ms)"
            );

            return timer;
        }

        /// <summary>
        /// Starts the sync timer if it exists and faction sync is still enabled.
        /// </summary>
        public static void StartSyncTimerIfEnabled(Timer timer, MainConfig config)
        {
            if (timer != null && !timer.Enabled && IsFactionSyncEnabled(config))
            {
                timer.Start();
                LoggerUtil.LogSuccess("Faction sync timer started");
            }
        }

        /// <summary>
        /// Stops the sync timer safely if it exists.
        /// </summary>
        public static void StopSyncTimer(Timer timer)
        {
            if (timer != null && timer.Enabled)
            {
                timer.Stop();
                LoggerUtil.LogInfo("Faction sync timer stopped");
            }
        }
    }
}
