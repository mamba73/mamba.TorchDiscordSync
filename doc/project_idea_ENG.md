# Idejni projekt: mamba.TorchDiscordSync

## 1) Torch / Space Engineers modul
**Svrha:** interakcija sa SE serverom preko Torch API-ja.

**Glavne odgovornosti:**
- Session lifecycle
    - Hook na ITorchSessionManager.SessionStateChanged
    - Reakcija samo na TorchSessionState.Loaded
- Faction reader
    - Dohvat svih frakcija iz SE svijeta
    - Čitanje: Faction ID, Tag, Name, Leader SteamID, Members SteamID
- Change detection
    - Periodički scan (npr. svakih 60s)
    - Detekcija novih/frakcija, promjena članstva ili lidera
- Output: normalizirani `FactionModel` objekt

## 2) Core Sync / Orchestration modul
**Svrha:** centralni mozak, povezuje podatke, ne poziva Torch ni Discord direktno.

**Glavne odgovornosti:**
- State management
    - Zadnje poznato stanje frakcija
    - Odlučuje što se promijenilo
- Rules engine
    - Svaka frakcija → Discord role
    - Svaka frakcija → Discord channel
    - Nickname: `[TAG] OriginalNick`
- Dispatch
    - Akcije → Discord modul
    - Promjene → Database modul

## 3) Discord modul
**Svrha:** komunikacija s Discord API-jem.

**Glavne odgovornosti:**
- Connection: inicijalizacija bota, safe reconnect
- Role management: kreiranje/brisanje role
- Channel management: kreiranje/brisanje channel, permissions
- Nickname sync: `[TAG] OriginalNick`, undo / rollback
- Idempotent behaviour: sigurno ponavljanje operacija

## 4) Database (SQLite) modul
**Svrha:** persistencija podataka.

**Glavne odgovornosti:**
- Schema: FactionID → DiscordRoleID, FactionID → DiscordChannelID, Player nick mapping
- Read / Write: load na startup, save nakon sync
- Soft delete polja: `DeletedAt` za undo
- Jedna SQLite connection instanca, thread safe

## 5) Security / Permissions modul
- SteamID whitelist
- Provjera prije izvršavanja komandi (Torch / Discord)
- Aktivacija anti-abuse u produkciji (Debug = false)

## 6) Commands modul (Torch chat / console)
- `/tds sync` – force full resync
- `/tds cleanup` – remove orphaned roles/channels
- `/tds status` – show current sync state
- `/tds reload` – reload config + database

## 7) Config modul
- Discord token
- Guild ID
- Sync interval
- Debug mode
- Security settings (SteamID whitelist)

---

**Napomena:**  
Sve promjene (nicknames, role/channel, timestamp) zapisivati u log.  
Database čuvati samo stanje za undo i rollback.  
Nickname na Discordu: `[TAG] OriginalNick`.
