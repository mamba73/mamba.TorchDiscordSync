// Utils/ChatUtils.cs
using System;

namespace mamba.TorchDiscordSync.Utils
{
    public static class ChatUtils
    {
        /// <summary>
        /// Pošalje poruku na server (console/chat)
        /// </summary>
        public static void SendServerMessage(string message)
        {
            try
            {
                // Direktno u console - može se kasnije integrirati sa IChatManager ako je dostupan
                Console.WriteLine($"[SERVER] {message}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Chat message send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pošalje broadcast poruku svim igračima
        /// </summary>
        public static void BroadcastToServer(string message)
        {
            try
            {
                Console.WriteLine($"[BROADCAST] {message}");
                // TODO: Integrirati sa IChatManager kada je dostupan
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Broadcast error: {ex.Message}");
            }
        }

        /// <summary>
        /// Odgovori na komandu
        /// </summary>
        public static void SendCommandResponse(string command, string result)
        {
            LoggerUtil.LogInfo($"[COMMAND] {command} → {result}");
            SendServerMessage($"✅ {result}");
        }

        /// <summary>
        /// Pošalje upozorenja
        /// </summary>
        public static void SendWarning(string message)
        {
            SendServerMessage($"⚠️ {message}");
        }

        /// <summary>
        /// Pošalje grešku
        /// </summary>
        public static void SendError(string message)
        {
            SendServerMessage($"❌ {message}");
        }

        /// <summary>
        /// Pošalje uspjeh poruku
        /// </summary>
        public static void SendSuccess(string message)
        {
            SendServerMessage($"✅ {message}");
        }
    }
}