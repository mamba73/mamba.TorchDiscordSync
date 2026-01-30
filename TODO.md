2026-01-30@12:22
Napraviti da ukoliko nadogradim aplikaciju, ukoliko konfiguracijska datoteka već postoji (xml) samo napravimo apply dodatnih parametara, a ne da se prebriše sa predefiniranim, kako bih izbjegao ponovno upisivanje svih parametara u konfiguraciju.


2026-01-28@09:50

Treba implementirati da poruke idu iz igre prema discordu i sa discorda u igru.
Na discordu treba paziti na blacklisted riječi; jer se ne smiju dozvoliti attachmenti, slanje linkova, slanje slika. Treba napraviti "moderaciju" i korisnik koji pošalje blacklisted poruku, na diskordu dobije opomenu. Nakon X opomena dobije mute na XX minuta. Ukoliko korisnik i nakon mute ponovno šalje blacklisted poruke nakon X puta; dobije isključenje sa kanala. X i XX je definirano u konfiguraciji kao i poruke za opomene, mute i isključenje sa kanala - ukoliko još nije, treba napraviti konfiguraciju. Sve se šalje kao log u admin kanal.

**Torch**: 1.3.1+  
**Space Engineers**: 1.208+  
**C#**: 4.6+ / .NET Framework 4.8

╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║         REVIDIRANA ANALIZA - mamba.TorchDiscordSync v2.0.131              ║
║                      SA PRIORITETIMA KORISNIKA                             ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝


═══════════════════════════════════════════════════════════════════════════════
🔴 PRIORITET #1: CHAT - OBOSTRANA SINKRONIZACIJA
═══════════════════════════════════════════════════════════════════════════════

ZAHTJEV:
  Discord ↔ Igra
  - Poruke iz Discorda trebaju doći u globalnu igru
  - Poruke iz igre trebaju doći na Discord
  - Format trebam biti konzistentan i čitljiv

TRENUTNO STANJE:

  ✓ Discord → Igra JE FUNKCIONALNO
    └─ DiscordBotService.OnMessageReceivedEvent je hookiran
    └─ ChatSyncService.SendDiscordMessageToGameAsync() postoji
    └─ Koristi MyVisualScriptLogicProvider.SendChatMessage()
    └─ Format: "[Discord] korisničko_ime: poruka"
    ✓ OVO RADI

  ✗ Igra → Discord NIJE FUNKCIONALNO
    └─ ChatSyncService.SendGameMessageToDiscordAsync() POSTOJI
    └─ ALI: Nema event listenera koji je poziva
    └─ ProcessChatMessage() u Plugin.cs POSTOJI ali se NIJE NIGDJE POZVAN
    └─ ITorchSession nema direktnog chat event-a
    └─ Trebam PRILAGOĐENU integraciju

DETALJNI PLAN ZA CHAT:

  STANJE #1: Kako detektirati chat igre?
  ──────────────────────────────────────
  
  Problem:
    - Torch nema event-a za chat poruke
    - Space Engineers chat ide kroz MultiplayerManager
    - Trebam alternativni pristup
  
  Opcija A: Polling na MultiplayerManager
    └─ Čitaj sve poruke iz session-a periodički
    └─ Detektuj nove poruke
    └─ Prosljeđuj na Discord
  
  Opcija B: Hook na IMyMultiplayer
    └─ MyMultiplayer.SendChatMessage event
    └─ Trebam reflection ili sličan pristup
  
  Opcija C: Torch integration
    └─ Torch ima ChatManager
    └─ Trebam vidjeti da li ima dostupnih event-a
  
  PREPORUKA: Hybrid approach
    └─ Čekaj da vidim što Torch nudi
    └─ Koristi dostupne event-e
    └─ Fallback na polling ako trebam

  STANJE #2: Gdje se chat obrađuje u kodu?
  ───────────────────────────────────────
  
  Plugin.ProcessChatMessage() JE SKELET:
  ```csharp
  public void ProcessChatMessage(string message, string author, string channel)
  {
      if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(author))
          return;

      // Proslijedi system poruke na player tracking
      if (channel == "System" && _playerTracking != null)
      {
          _playerTracking.ProcessSystemChatMessage(message);
      }

      // Normalne poruke (global) → pošalji na Discord
      if (_chatSync != null && _config?.Chat != null)
      {
          bool serverToDiscordEnabled = _config.Chat.ServerToDiscord;
          
          if (serverToDiscordEnabled)
          {
              if (message.StartsWith("/") || channel == "System")
              {
                  return; // Preskoči komande
              }

              _ = _chatSync.SendGameMessageToDiscordAsync(author, message);
          }
      }
  }
  ```
  
  ✓ LOGIKA JE ISPRAVNA
  ✗ ALI SE NIGDJE NE POZIVA
  
  Trebam:
  - Napraviti ChatIntegrationService
  - Hookirati na pravi Torch event ili fallback
  - Pozivati ProcessChatMessage() kada se detektuje poruka

  STANJE #3: Discord poruke već dolaze
  ────────────────────────────────────
  
  U Plugin.Init():
  ```csharp
  if (_discordBot != null)
  {
      _discordBot.OnMessageReceivedEvent += async msg =>
      {
          if (msg.Channel is SocketTextChannel textChannel
              && textChannel.Id == _config.Discord.ChatChannelId)
          {
              if (msg.Author.IsBot || msg.Content.StartsWith(_config.Discord.BotPrefix))
                  return;

              await _chatSync.SendDiscordMessageToGameAsync(
                  msg.Author.Username,
                  msg.Content
              );
          }
      };
  }
  ```
  
  ✓ OVO RADI - Discord → Igra je funkcionalno
  ✓ Poruke dolaze kao server broadcast
  ✓ Format je OK

ZAKLJUČAK ZA CHAT:

  ✓ 50% gotovo (Discord → Igra)
  ✗ 50% nedostaje (Igra → Discord integration)
  
  TREBAM NAPRAVITI:
  1. ChatIntegrationService koji detektira chat poruke
  2. Hookiranje na Torch event ili fallback mehanizam
  3. Konekcija na ProcessChatMessage()
  4. Testiranje obostrane sinkronizacije

  ČEKA: Trebam znati što Torch nudi za chat detection


═══════════════════════════════════════════════════════════════════════════════
🔴 PRIORITET #2: DEATH DETECTION (sa detaljima)
═══════════════════════════════════════════════════════════════════════════════

ZAHTJEV:
  Trebam znati:
  1. TKO je ubio (killer name)
  2. KOGA je ubio (victim name)
  3. SA ČIME je ubio (weapon type)
  4. GDJE se to desilo (location - planet, asteroid, koordinate)
  
  Trebam:
  - Istu poruku na Discordu I u chat igre (globalno)
  - Klasifikaciju smrti (Suicide/FirstKill/Retaliation/itd)
  - Spremi sve u XML bazu

TRENUTNO STANJE:

  ✓ DeathLogService.LogPlayerDeathAsync() POSTOJI
    └─ Prima sve potrebne podatke
    └─ Klasificira smrt (DeathTypeEnum)
    └─ Generiše Discord poruku
    └─ Sprema u DB

  ✓ DeathHistoryModel.cs JE DOBAR
    ```csharp
    public class DeathHistoryModel
    {
        public long KillerSteamID { get; set; }
        public long VictimSteamID { get; set; }
        public DateTime DeathTime { get; set; }
        public string DeathType { get; set; }
        public string KillerName { get; set; }
        public string VictimName { get; set; }
        public string Weapon { get; set; }
        public string Location { get; set; }
    }
    ```
    ✓ Ima sve što trebam

  ✓ EventLoggingService.LogDeathAsync() POSTOJI
    └─ Prosljeđuje na Discord u ChatChannelId

  ✗ DETEKTOVANJE SMRTI JE POKVARENO
    └─ PlayerTrackingService koristi reflection na MyCharacter.Die()
    └─ MyCharacter.Die() ne prosljeđuje info o napasaču
    └─ Rezultat: Unknown killer, Unknown weapon
    └─ Smrt se bilježi samo prvi put (bug?)
    └─ Nema podatka o lokaciji

  ✗ NEMA PORUKE U CHAT IGRE
    └─ Poruka ide samo na Discord
    └─ Trebam slati i u globalnu igru
    └─ Trebam koristiti MyVisualScriptLogicProvider.SendChatMessage()

KAKO DETEKTIRATI SMRT - OPCIJE:

  OPCIJA A: Chat Message Parsing (PREPORUKA: Trebam OVO)
  ────────────────────────────────────────────────────
  Space Engineers javlja smrt u chat-u:
    "PlayerA was killed by PlayerB using Rocket"
    "PlayerA died in an explosion"
    "PlayerA was killed by Armor"
  
  Trebam:
  - Regex parser koji hvata ove poruke
  - Izvuči: victim, killer, weapon
  - Logira u DeathLogService
  
  Prednosti:
    ✓ Jednostavno
    ✓ Pouzdano
    ✓ Ima sve info
  
  Mane:
    ✗ Trebam znati točan format poruke
    ✗ Trebam testirati na live serveru

  OPCIJA B: Damage Tracking
  ──────────────────────────
  Trebam hookarati MySyncDamage.DoDamageSynced()
  Zapamtiti: tko je dao damage, sa čime
  Kada umre, vidjeti tko je dao zadnji damage
  
  Prednosti:
    ✓ Točan killer
    ✓ Točno weapon
  
  Mane:
    ✗ Komplicirano reflection
    ✗ Trebam znati što je MySyncDamage
    ✗ Trebam znati verziju SE DLL-a

  OPCIJA C: Hybrid approach (BEST)
  ────────────────────────────────
  1. Chat message parsing kao PRIMARY
  2. Damage tracking kao fallback
  3. Unknown kao last resort

GDJE DETEKTIRATI SMRT:

  Trenutno: PlayerTrackingService.OnCharacterDied()
  Problem: Nema info o killeru
  
  Trebam:
  - Proširiti sa chat message parsing-om
  - Parsirati poruke iz ProcessChatMessage()
  - Proslijediti detaljne death info umjesto samo imena

GDJE SLATI PORUKU:

  Trenutno: Samo na Discord
  
  Trebam:
  - Slati i u globalnu igru
  - Koristiti MyVisualScriptLogicProvider.SendChatMessage()
  - Format: "PlayerA was killed by PlayerB with Rocket"

ZAKLJUČAK ZA DEATH:

  ✗ 20% gotovo (backend logika)
  ✗ 80% nedostaje (detection + broadcast poruke)
  
  TREBAM NAPRAVITI:
  1. DeathMessageParser za chat poruke igre
  2. Location resolver (za gdje se smrt desila)
  3. Broadcast u chat igre (MyVisualScriptLogicProvider)
  4. Integration sa postojećim DeathLogService
  5. Testiranje na live serveru

  ČEKA: Trebam znati točan format death poruka u chat-u


═══════════════════════════════════════════════════════════════════════════════
🔴 PRIORITET #3: FACTION SYNC NA DISCORD
═══════════════════════════════════════════════════════════════════════════════

ZAHTJEV:
  1. Čitaj faction-e iz Torch API (ne iz save data)
  2. Kreiraj Discord role po faction-u (tag je ime role)
  3. Kreiraj Discord channel po faction-u
  4. Dodaj player-e u role kada se verificiraju
  5. Periodički sync promjena

TRENUTNO STANJE:

  ✓ FactionSyncService.cs STRUKTURA JE OK
    └─ SyncFactionsAsync() je implementirana
    └─ ResetDiscordAsync() je implementirana
    └─ DatabaseService.SaveFaction() postoji

  ✓ DiscordBotService ima:
    └─ CreateRoleAsync(roleName)
    └─ CreateChannelAsync(channelName)
    └─ AssignRoleAsync(userID, roleID)
    └─ DeleteRoleAsync(roleID)

  ✗ NEMA TORCH API INTEGRACIJE
    Kao što si rekao:
    ```csharp
    private List<FactionModel> LoadFactionsFromSession(ITorchSession session)
    {
        var testFaction = new FactionModel();
        testFaction.Tag = "ABC";
        testFaction.Name = "Test Faction";
        // ... TEST DATA SAMO
        return factions;
    }
    ```
    ❌ Koristi TEST FACTION-E!

  ✗ NEMA KREIRANJA ROLE IZ TEST PODATAKA
    └─ Čak ni test faction-i se ne kreiraju na Discordu
    └─ Kod za Discord role creation nije pozvan pravilno

  ✗ NEMA DODJELE PLAYER → ROLE
    └─ Trebam znati koji Discord user je koji player
    └─ Trebam dodijeliti role kada se verificira
    └─ Trebam update role kada se faction promijeni

  ✗ NEMA PERIODIC SYNC-A
    └─ Trebam detektirati promjene na serveru
    └─ Trebam update role/channel-e
    └─ Trebam obrisati izbrisane faction-e

DETALJNI PLAN:

  KORAK 1: Čitaj faction-e iz Torch API
  ─────────────────────────────────────
  
  Trebam:
  - MyAPIGateway.Factions (trebam znati interface)
  - Sve player-e sa njihovim faction-ima
  - Filter: tag.length == 3 (samo player faction-i)
  
  Trebam pisati:
  - FactionReaderService
  - Koji čita iz MyAPIGateway
  - Vraća List<FactionModel>

  KORAK 2: Kreiraj Discord role
  ────────────────────────────
  
  Za svaki faction:
  - Kreiraj role sa imenom = tag (ABC)
  - Spremi role ID u DB
  - Spremi mapping: factionID → roleID
  
  Trebam:
  - Koristiti DiscordBotService.CreateRoleAsync()
  - Proslijediti tag kao ime role
  - Čuvati DiscordRoleID u FactionModel

  KORAK 3: Kreiraj Discord channel-e
  ──────────────────────────────────
  
  Za svaki faction:
  - Kreiraj channel sa imenom = faction name (lowercase)
  - Postavi permissions: samo ta role može vidjeti
  - Spremi channel ID u DB
  
  Trebam:
  - DiscordBotService.CreateChannelAsync()
  - Proslijediti faction name
  - Postaviti OverWrites za role
  - Čuvati DiscordChannelID u FactionModel

  KORAK 4: Dodjeli role player-u
  ─────────────────────────────
  
  Kada se player verificira:
  - Saznaj njegovu faction
  - Saznaj Discord role ID faction-a
  - Dodjeli role Discord user-u
  
  Trebam:
  - VerificationService integration
  - GetPlayerFaction() logika
  - DiscordBotService.AssignRoleAsync()

  KORAK 5: Periodic sync
  ─────────────────────
  
  Svakih N sekundi:
  - Čitaj sve faction-e sa servera
  - Detektuj nove
  - Detektuj izbrisane
  - Update promjene
  
  Trebam:
  - Loop koji čita svake N sekundi
  - Diff algorithm: prije vs sada
  - Create/Delete/Update logika

ZAKLJUČAK ZA FACTION:

  ✗ 40% gotovo (Discord backend)
  ✗ 60% nedostaje (Torch API + integration)
  
  TREBAM NAPRAVITI:
  1. FactionReaderService (čitanje iz Torch API)
  2. Faction → Discord role mapping
  3. Player → Role assignment (verify integration)
  4. Periodic sync loop
  5. Cleanup (brisanje faction-a)

  ČEKA: Trebam znati MyAPIGateway.Factions interface


═══════════════════════════════════════════════════════════════════════════════
🟡 PRIORITET #4: KOMANDE (nakon što su gornja 3 gotova)
═══════════════════════════════════════════════════════════════════════════════

ZAHTJEV:
  User komande:
    /tds help
    /tds verify @DiscordUsername
  
  Admin komande:
    /tds status
    /tds sync
    /tds reset
    /tds cleanup
    /tds reload
    /tds unverify SteamID

TRENUTNO STANJE:

  ✓ CommandProcessor.cs POSTOJI
    └─ Svi handler-i su implementirani
    └─ Admin check-ovi su na mjestu
  
  ⚠️  Status: Trebam TESTIRATI nakon što su faction-i gotovi
  ⚠️  Sync: Trebam TESTIRATI nakon što su faction-i gotovi
  ⚠️  Reset: Je li sigurna?
  ✗ Cleanup: Nije završeno
  ⚠️  Verify/Unverify: Trebam znati kako VerificationService radi

ZAKLJUČAK ZA KOMANDE:

  ~ 50% gotovo (svi skeleton-i su napisani)
  ~ 50% trebam testirati i dovršiti

  Čeka se: Testiranje nakon što su chat/death/faction gotova


═══════════════════════════════════════════════════════════════════════════════
📋 SAŽETAK - REDOSLIJED IMPLEMENTACIJE
═══════════════════════════════════════════════════════════════════════════════

FAZA 1: CHAT (Trebam prvo)
──────────────────────────
Trebam:
  1. ChatIntegrationService - detektira chat poruke
  2. Event hooking na Torch ili fallback mehanizam
  3. Konekcija na ProcessChatMessage()
  4. Testiranje: Discord ↔ Igra

Vrijeme: ~4-6 sati

FAZA 2: DEATH DETECTION (Trebam drugo)
──────────────────────────────────────
Trebam:
  1. DeathMessageParser - parsira chat za death poruke
  2. Location resolver - zna gdje se smrt desila
  3. Broadcast u chat igre - slanja poruku svima
  4. Integration sa DeathLogService
  5. Testiranje: Discord + chat igre

Vrijeme: ~4-6 sati

FAZA 3: FACTION SYNC (Trebam treće)
───────────────────────────────────
Trebam:
  1. FactionReaderService - čita iz Torch API
  2. Role creation logika
  3. Channel creation logika
  4. Player → Role assignment (verify integration)
  5. Periodic sync loop
  6. Testiranje: Faction-i se kreiraju na Discordu

Vrijeme: ~6-8 sati

FAZA 4: KOMANDE (Trebam četvrto)
────────────────────────────────
Trebam:
  1. Testiranje svih komandi
  2. VerificationService integration
  3. Cleanup završavanje
  4. Edge case handling

Vrijeme: ~2-3 sata

UKUPNO: ~16-23 sata kod pisanja


═══════════════════════════════════════════════════════════════════════════════
🎯 ŠTA TREBAM ZNATI PRE NEGO KRENEŠ SA KODOM
═══════════════════════════════════════════════════════════════════════════════

CHAT:
  1. Kako Torch detektira player chat poruke?
  2. Koji event ili method trebam hookarati?
  3. Što je dostupno iz ITorchSession?

DEATH:
  1. Koji je točan format death poruke u chat-u?
  2. Trebam regex pattern za parsing?
  3. Kako znam gdje je player bio? (WorldMatrix?)

FACTION:
  1. Kako pristupiti MyAPIGateway.Factions?
  2. Koja je struktura IFaction i IFactionMember?
  3. Kako znam koja faction je obrisana?

KOMANDE:
  1. Kako VerificationService radi?
  2. Trebam novi kod ili je integration dovoljna?
  3. Je li cleanup sigurna komanda?


═══════════════════════════════════════════════════════════════════════════════
ČEKAM TVOJ FEEDBACK
═══════════════════════════════════════════════════════════════════════════════

1. Je li redoslijed ispravan (Chat → Death → Faction → Komande)?
2. Jesu li moje procjene o što trebam sigurne?
3. Možeš li odgovoriti na gornja pitanja?
4. Trebam li početi sa FAZA 1 (Chat)?