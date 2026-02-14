Glavne promjene koje sam implementirao:
1. Server Up/Down Messages:
NEW: SendServerStartupMessage() - šalje poruku iz _config.Monitoring.ServerStartedMessage
NEW: SendServerShutdownMessage() - šalje poruku iz _config.Monitoring.ServerStoppedMessage
FIXED: Registriran TorchSessionState.Unloaded state za shutdown poruku
2. Player Joined/Left Messages:
NEW: RegisterPlayerEvents() - registrira connect/disconnect evente
NEW: OnPlayerConnected() - šalje _config.Chat.JoinMessage na Discord
NEW: OnPlayerDisconnected() - šalje _config.Chat.LeaveMessage na Discord
3. Death Messages:
NEW: RegisterDeathEvents() - registrira death event
NEW: OnPlayerDied() - procesira death event i šalje poruku
NEW: BuildDeathMessage() - kreira detaljnu poruku na temelju MyDamageInformation
4. Real SimSpeed:
NEW: GetSimSpeedFromSession() - dohvaća stvarni SimSpeed iz sesije
5. Tracking:
NEW: _serverShutdownSent flag - sprečava duplikate shutdown poruka
Sve poruke se sada koriste iz tvoje konfiguracije u MainConfig.cs i šalju se na Discord ako su omogućene u odgovarajućim sekcijama.