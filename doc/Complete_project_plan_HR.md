# mamba.TorchDiscordSync.Plugin - KOMPLETAN IDEJNI PLAN

## 📋 SAŽETAK PROJEKTA

**Cilj:** Sinkronizacija Space Engineers servera sa Discord serverom
- Logiranje igrača (join/leave)
- Sinkronizacija smrti sa random porukama
- Chat sinkronizacija (globalni chat ↔ Discord)
- Verifikacija korisnika (Steam ↔ Discord)
- Upravljanje fakcijama

**Status:** 40% Gotovo, 60% Trebalo bi da radi ali ima bugova

---

## 📁 DATOTEKE - ŠTA RADI, ŠTA NE

### 🔧 **CORE SERVICES** (Infrastruktura)

#### 1. `Services/DatabaseService.cs`
- **Što radi:** XML-based baza podataka (XML fajlovi umjesto SQL)
- **Funkcionalnost:** SaveFaction(), GetFaction(), SavePlayer(), GetPlayer()
- **Status:** ✅ RADI - no provjera trebala
- **Trebalo bi:** Optimizacija XML read/write operacija

#### 2. `Services/DiscordBotService.cs`
- **Što radi:** Discord bot WebSocket konekcija i upravljanje
- **Funkcionalnost:** ConnectAsync(), SendMessageAsync(), DeleteRoleAsync()
- **Status:** ⚠️ **PROBLEM - Bot se disconnecta i ne reconnecta automatski**
- **Log:** `[WARN] [DISCORD_BOT] Bot disconnected: WebSocket connection was closed`
- **Trebalo bi:**
  - ✅ Automatski reconnect kada se disconnecta
  - ✅ Error handling kada bot nije dostupan
  - ✅ Validacija tokena prije connect-a

#### 3. `Services/DiscordService.cs`
- **Što radi:** Wrapper oko Discord.NET biblioteke
- **Funkcionalnost:** CreateRoleAsync(), CreateChannelAsync(), SendMessageAsync()
- **Status:** ⚠️ **PROBLEM - Ne može slati poruke kada je bot disconnectan**
- **Trebalo bi:** Error handling kada bot nije dostupan

#### 4. `Services/EventLoggingService.cs`
- **Što radi:** Logiranje svih事件a na Discord (join, leave, death, status)
- **Funkcionalnost:** LogPlayerJoinAsync(), LogPlayerLeaveAsync(), LogDeathAsync(), LogServerStatusAsync()
- **Status:** ⚠️ **PROBLEM - LogDeathAsync šalje samo fallback poruku "player died"**
- **Razlog:** DeathMessageHandler je NULL kada se poziva
- **Trebalo bi:** Popraviti redoslijed inicijalizacije

#### 5. `Services/PlayerTrackingService.cs`
- **Što radi:** Praćenje igrača (join/leave) i smrti
- **Funkcionalnost:** 
  - `OnPollingTick()` - Detektuje join/leave svakih 5 sekundi
  - `OnCharacterDied()` - Hook za smrt karaktera
  - `InitializeDeathTracking()` - Hookiraj sve karaktere
- **Status:** 
  - ✅ Join/Leave - **RADI**
  - ❌ Death messages - **NE RADI - DeathMessageHandler je null**
- **Trebalo bi:** DeathMessageHandler mora biti inicijaliziran PRIJE PlayerTrackingService

#### 6. `Services/ChatSyncService.cs`
- **Što radi:** Sinkronizacija globalnog chata između igre i Discorda
- **Funkcionalnost:** SendGameMessageToDiscordAsync(), SendDiscordMessageToGameAsync()
- **Status:** 
  - ✅ Discord→Game - **RADI**
  - ⚠️ Game→Discord - **RADI ALI STANE nakon što se bot disconnecta**
- **Trebalo bi:** Fallback kada bot nije dostupan

#### 7. `Services/DeathLogService.cs`
- **Što radi:** Logiranje smrti u bazu i Discord
- **Funkcionalnost:** LogDeathAsync()
- **Status:** ⚠️ **PROBLEM - Logira samo fallback poruku**
- **Trebalo bi:** Trebalo bi da prima DeathMessage, ne samo generira "player died"

#### 8. `Services/DeathLocationService.cs`
- **Što radi:** Određivanje lokacije gdje se igrač umro (planeta, kosmos, etc.)
- **Funkcionalnost:** GetDeathZoneInfo(), GetPlanetName()
- **Status:** ✅ **GOTOVO** (prethodno je bilo s greškama, sada je OK)
- **Trebalo bi:** Testiranje sa različitim lokacijama

#### 9. `Services/FactionSyncService.cs`
- **Što radi:** Sinkronizacija fakcija između igre i Discorda
- **Funkcionalnost:** LoadFactionsFromGame(), SyncFactionsAsync(), ResetDiscordAsync()
- **Status:** ✅ **GOTOVO** - No trebalo by testiranja
- **Trebalo bi:** Testiranje sa stvarnim fakcijama

#### 10. `Services/VerificationService.cs`
- **Što radi:** Verifikacija Steam korisnika na Discordu
- **Funkcionalnost:** GenerateVerificationCode(), VerifyCode()
- **Status:** ✅ **GOTOVO**
- **Trebalo bi:** Testiranje sa pravim korisnicima

---

### 🎯 **HANDLERS** (Logika obrade)

#### 1. `Handlers/CommandProcessor.cs`
- **Što radi:** Obrađuje /tds komande (verify, status, help, sync, reset)
- **Funkcionalnost:**
  - `ProcessCommand()` - Route komande
  - `HandleVerifyCommand()` - Generira kod za verifikaciju
  - `HandleStatusCommand()` - Prikazuje status
  - `HandleHelpCommand()` - Prikazuje dostupne komande
- **Status:** ✅ **RADI** (testovano)
- **Trebalo bi:** Sve komande rade ispravno

#### 2. `Handlers/ChatModerator.cs`
- **Što radi:** Filtrira chat poruke (moderacija, blacklist, mute, kick)
- **Funkcionalnost:**
  - `ProcessDiscordMessage()` - Obradi Discord poruke sa moderacijom
  - `ProcessGameMessage()` - Obradi game poruke
  - `ShouldBlockGameMessage()` - Fitrira death messages, duplikate
  - `IsDeathMessage()` - Detektuje death messages čitanjem emoticona iz config-a
  - `IsRecentDuplicate()` - Time-based dedup (3 sekunde)
- **Status:** ✅ **RADI** (death messages se blokiraju, dedup radi)
- **Trebalo bi:** Testiranje moderacije (blacklist, warnings, mute)

#### 3. `Handlers/DeathMessageHandler.cs`
- **Što radi:** Generira random death message iz template-a
- **Funkcionalnost:**
  - `HandlePlayerDeathAsync()` - Glavni handler za smrt
  - `GenerateUnifiedDeathMessage()` - Kreira poruku iz DeathMessagesConfig
  - `AddEmotePrefix()` - Dodaj random emote iz config-a
  - `SendToGameChat()` - Pošalji u igru
  - `SendToDiscordAsync()` - Pošalji na Discord
- **Status:** ❌ **NE RADI - Handler je NULL**
- **Trebalo bi:** 
  - Inicijalizacija PRIJE PlayerTrackingService
  - Logiranje što se desava sa svakom porukom

#### 4. `Handlers/EventManager.cs`
- **Što radi:** Upravljanje server event-ima
- **Status:** ✅ **Postoji** (nije korišten u log-u)
- **Trebalo bi:** Trebalo bi da se koristi

---

### ⚙️ **UTILITIES** (Pomoćne funkcije)

#### 1. `Utils/ChatUtils.cs`
- **Što radi:** Slanje poruka igračima
- **Funkcionalnost:**
  - `SendToPlayer()` - Pošalji u chat igračima
  - `SendError()`, `SendSuccess()`, `SendInfo()`, `SendWarning()` - Sa emoticonima
  - `ProcessChatMessage()` - Obradi chat poruku za Discord
  - `HandleChatCommand()` - Proslijedi komandu CommandProcessor-u
- **Status:** ✅ **RADI** (ali trebalo bi da se provjerite sve metode)
- **Trebalo bi:** Testiranje sa stvarnim porukama

#### 2. `Utils/PluginUtils.cs`
- **Što radi:** Helper metode za plugin
- **Funkcionalnost:**
  - `PrintBanner()` - Ispis bannera
  - `GetCurrentSimSpeed()` - Dobivanje game simulation speed
  - `CreateSyncTimerIfEnabled()` - Kreiranje sync timera
  - `IsFactionSyncEnabled()` - Provjera konfiguracije
- **Status:** ✅ **RADI**
- **Trebalo bi:** OK

#### 3. `Utils/LoggerUtil.cs`
- **Što radi:** Logging sa bojevima za debug
- **Status:** ✅ **RADI**
- **Trebalo bi:** OK

---

### 📦 **CONFIG** (Konfiguracija)

#### 1. `Config/MainConfig.cs`
- **Što radi:** Glavna konfiguracija sa svim postavkama
- **Strukturu:**
  - `Discord` - Bot token, channel IDs, guild ID
  - `Chat` - Enable/disable chat sync, moderacija, blacklist
  - `Death` - DeathMessageEmotes, dedup window
  - `Faction` - Enable/disable faction sync
  - `Monitoring` - Server status logging
  - `Verification` - Code expiration
- **Status:** ✅ **GOTOVO**
- **Trebalo bi:** Provjerite konfiguraciju je li sve postavljeno

#### 2. `Config/DeathMessagesConfig.cs`
- **Što radi:** Random death message templates
- **Primjer:**
  ```xml
  <Accident>
    <Template>{victim} opened the hunting season!</Template>
    <Template>{victim} became fish food!</Template>
  </Accident>
  ```
- **Status:** ✅ **GOTOVO**
- **Trebalo bi:** Više template-a za raznolikost

---

### 🎮 **PLUGIN** (Glavna datoteka)

#### `Plugin/MambaTorchDiscordSyncPlugin.cs`
- **Što radi:** Glavna plugin klasa - inicijalizacija i event handling
- **Ključne metode:**
  - `Init()` - Inicijalizacija svih servisa
  - `OnSessionStateChanged()` - Detektuje kada se server učita/isključi
  - `OnChatMessageProcessing()` - Glavno chat event-om handler
  - `OnServerLoadedAsync()` - Startup rutine
- **Status:** ⚠️ **PROBLEM - Redoslijed inicijalizacije**
- **Trebalo bi:**
  - **KRITIČNO:** Kreiraj `_deathMessageHandler` PRIJE `_playerTracking`
  - Dodaj better error handling
  - Reconnect logika za Discord bot

---

## 🛠️ ŠTA JE IMPLEMENTIRANO A NE RADI

### 🔴 **KRITIČNI PROBLEMI** (Trebalo Hitno Fixati)

#### 1. **DeathMessageHandler je NULL** 
- **Gdje:** `Services/PlayerTrackingService.cs` -> `OnCharacterDied()`
- **Log:** `[WARN] [DEATH] DeathMessageHandler is null - using fallback`
- **Uzrok:** `_deathMessageHandler` se inicijalizira NAKON `_playerTracking`
- **Fix:**
  ```csharp
  // U Plugin Init():
  // PRIJE PlayerTrackingService:
  _deathMessageHandler = new DeathMessageHandler(_eventLog, _config);
  
  // POSLIJE:
  _playerTracking = new PlayerTrackingService(
      _eventLog, _torch, _deathLog, _config, _deathMessageHandler
  );
  ```
- **Rezultat nakon fixa:** Death messages će biti random, ne "mamba died"

#### 2. **Discord Bot se Disconnecta i Ne Reconnecta**
- **Gdje:** `Services/DiscordBotService.cs`
- **Log:** `[WARN] [DISCORD_BOT] Bot disconnected: WebSocket connection was closed`
- **Problem:** Nema auto-reconnect logike
- **Fix:** Trebalo bi dodati u DiscordBotService:
  ```csharp
  private async Task ReconnectAsync()
  {
      while (!_client.Connected)
      {
          try
          {
              await ConnectAsync();
              break;
          }
          catch
          {
              await Task.Delay(5000); // Čekaj 5 sekundi pa pokušaj ponovo
          }
      }
  }
  ```
- **Rezultat nakon fixa:** Game→Discord poruke će nastaviti da rade čak i nakon disconnect-a

#### 3. **Game→Discord Poruke Stanu Kada Bot Bude Disconnectan**
- **Gdje:** `Services/ChatSyncService.cs` -> `SendGameMessageToDiscordAsync()`
- **Log:** `[WARN] Chat: Failed to send to Discord channel 1458392687647526966`
- **Problem:** Nema error handling kada bot nije dostupan
- **Fix:** Trebalo bi dodati fallback logiku ili queuing
- **Rezultat nakon fixa:** Poruke će biti stored i poslane kada se bot reconnecta

---

### 🟡 **MINOR PROBLEMI** (Trebalo Trebalo Testirati)

#### 1. **Death Location se Ne Koristi**
- **Status:** DeathLocationService je gotov ali se ne koristi u porukama
- **Trebalo bi:** Proslijediti lokaciju u DeathMessageHandler

#### 2. **Faction Sync Nije Testiran**
- **Status:** FactionSyncService je gotov ali trebalo by testiranja
- **Trebalo bi:** Pokrenuti /tds sync i provjeriti da li radi

#### 3. **Verification Nije Testiran**
- **Status:** VerificationService je gotov ali trebalo by testiranja
- **Trebalo bi:** Pokrenuti /tds verify i provjeriti kod

---

## 📊 STATUS SUMMARY

| Komponenta | Status | Problem | Priority |
|-----------|--------|---------|----------|
| Database | ✅ OK | - | - |
| Discord Bot | ⚠️ Problem | Ne reconnecta | KRITIČNO |
| Discord Service | ⚠️ Problem | Ne šalje kada bot down | KRITIČNO |
| Event Logging | ⚠️ Problem | Fallback poruka | KRITIČNO |
| Player Tracking | ✅ Join/Leave OK | Death handler NULL | KRITIČNO |
| Death Handler | ❌ NULL | Nije inicijaliziran prije | KRITIČNO |
| Chat Moderator | ✅ OK | - | - |
| Chat Sync | ⚠️ Problem | Stane nakon disconnect | KRITIČNO |
| Commands | ✅ OK | - | - |
| Faction Sync | ✅ OK (untested) | Trebalo testiranja | NIZAK |
| Verification | ✅ OK (untested) | Trebalo testiranja | NIZAK |
| Config | ✅ OK | - | - |

---

## 🎯 ACTION PLAN - REDOSLIJED FIKSANJA

### FAZA 1: KRITIČNI FIXESI (24h) ⏰

#### 1. **Fix DeathMessageHandler Initialization** (Prioritet: 🔴 KRITIČNO)
- **Datoteka:** `Plugin/MambaTorchDiscordSyncPlugin.cs`
- **Gdje:** `Init()` metoda
- **Što:** Kreiraj `_deathMessageHandler` PRIJE `_playerTracking`
- **Očekivani rezultat:** Death messages će biti random poruke, ne "mamba died"
- **Test:** Umri u igri - trebalo bi da vidiš random poruku

#### 2. **Add Discord Bot Reconnect Logic** (Prioritet: 🔴 KRITIČNO)
- **Datoteka:** `Services/DiscordBotService.cs`
- **Što:** Dodaj `ReconnectAsync()` metodu
- **Gdje:** U `OnDisconnected` event
- **Očekivani rezultat:** Bot će se automatski reconnectati
- **Test:** Simuliraj disconnect - trebalo bi da se reconnecta

#### 3. **Add Error Handling When Bot is Down** (Prioritet: 🔴 KRITIČNO)
- **Datoteka:** `Services/ChatSyncService.cs` i `Services/DiscordService.cs`
- **Što:** Dodaj try-catch i fallback logiku
- **Očekivani rezultat:** Game poruke neće biti izgubljene
- **Test:** Isključi bot - poruke trebale bi da se queue-uje ili logiraju

---

### FAZA 2: TESTIRANJE (48h) 🧪

#### 1. **Test Death Messages**
- Umri u igri
- Provjeraj:
  - ✅ Poruka se pojavljuje u igri
  - ✅ Poruka ide na Discord
  - ✅ Poruka je random (ne "mamba died")

#### 2. **Test Chat Sync**
- Pošalji poruku iz igre
- Provjeraj:
  - ✅ Poruka ide na Discord
  - ✅ Poruka sa Discorda dolazi u igru
  - ✅ Nema loop-ova

#### 3. **Test Commands**
- Izvršи `/tds verify`, `/tds status`, `/tds help`
- Provjeraj:
  - ✅ Svaka komanda radi
  - ✅ Poruke su vidljive igračima

#### 4. **Test Faction Sync**
- Izvršи `/tds sync` (ako je admin)
- Provjeraj:
  - ✅ Fakcije se učitavaju iz igre
  - ✅ Discord kanali/role se kreiraju

#### 5. **Test Verification**
- Izvršи `/tds verify username`
- Provjeraj:
  - ✅ Kod se generiše
  - ✅ Kod dolazi u Discord DM

---

### FAZA 3: OPTIMIZATION (Week 2) 🚀

#### 1. **Add Message Queuing**
- Ako bot bude down, queue poruke
- Pošalji kada se bot reconnecta

#### 2. **Improve Error Messages**
- Bolji logging za debug
- Bolji error messages za igrače

#### 3. **Performance Optimization**
- Reduce database read/write operations
- Cache factions

---

## 📋 CHECKLIST ZA IMPLEMENTACIJU

### FAZA 1: Kritični Fixesi

- [ ] **Fix 1: DeathMessageHandler Initialization**
  - [ ] Pronađi `Init()` metodu u Plugin
  - [ ] Pronađi gdje se kreira `_deathMessageHandler`
  - [ ] Pronađi gdje se kreira `_playerTracking`
  - [ ] Premjesti `_deathMessageHandler` inicijalizaciju PRIJE `_playerTracking`
  - [ ] Dodaj log za debug
  - [ ] Build
  - [ ] Test: Umri - trebalo bi random poruka

- [ ] **Fix 2: Discord Bot Reconnect**
  - [ ] Pronađi `DiscordBotService.cs`
  - [ ] Pronađi gdje se handleira disconnect
  - [ ] Dodaj `ReconnectAsync()` metodu
  - [ ] Testiraj: Simuliraj disconnect i provjeri reconnect

- [ ] **Fix 3: Chat Error Handling**
  - [ ] Pronađi `ChatSyncService.cs`
  - [ ] Dodaj try-catch u `SendGameMessageToDiscordAsync()`
  - [ ] Dodaj fallback logiku
  - [ ] Testiraj: Isključi bot i provjeri error handling

### FAZA 2: Testiranje

- [ ] Test Death Messages
- [ ] Test Chat Sync (Game→Discord i Discord→Game)
- [ ] Test Commands (/tds verify, /tds status, /tds help)
- [ ] Test Faction Sync (ako je dostupno)
- [ ] Test Verification

### FAZA 3: Dokumentiranje

- [ ] Update README sa novim features
- [ ] Dokumentiraj svaki fix sa razlogom
- [ ] Commit sa jasnim porukama

---

## 🔗 DATOTEKE NA GITHUB-u

**Branch:** `dev3`
**Link:** https://github.com/mamba73/mamba.TorchDiscordSync.Plugin/tree/dev3

**Ključne datoteke za fix:**
1. `/Plugin/MambaTorchDiscordSyncPlugin.cs` - DeathMessageHandler init
2. `/Services/DiscordBotService.cs` - Bot reconnect
3. `/Services/ChatSyncService.cs` - Error handling
4. `/Services/PlayerTrackingService.cs` - Death messages
5. `/Handlers/DeathMessageHandler.cs` - Random poruke