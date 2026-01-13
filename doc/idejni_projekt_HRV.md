
# mamba.TorchDiscordSync

Autor: mamba  
Trenutna verzija: 0.1.0  
Torch verzija: 1.3.1.328-master  
Space Engineers verzija: 1.208.15

## Pregled
Ovaj plugin sinkronizira **Space Engineers player frakcije** s Discord serverom.
Omogućuje:
- Kreiranje Discord rola po frakciji
- Kreiranje Discord text / forum kanala po frakciji
- Evidenciju mapiranja u SQLite bazi

**Status:** 🚧 Početna buildable verzija

---

## 1) Torch / Space Engineers modul
**Svrha**  
Interakcija sa SE serverom preko Torch API-ja.

**Glavne odgovornosti**  
- **Session lifecycle**: hook na `ITorchSessionManager.SessionStateChanged`, reagiranje samo na `TorchSessionState.Loaded`.  
- **Faction reader**: dohvat svih frakcija, tag, name, leader, članovi (SteamID).  
- **Change detection**: periodični scan, usporedba sa prethodnim snapshotom, detekcija novih/frakcija/promjena članstva/lidera.  
- **Output**: `FactionModel` objekt → Core Sync Layer.

---

## 2) Core Sync / Orchestration modul
**Svrha**  
Središnji mozak plugina, ne zna ništa o Torchu ni Discordu.

**Glavne odgovornosti**  
- **State management**: drži zadnje poznato stanje frakcija, odlučuje što se promijenilo.  
- **Rules engine**: pravila (svaka frakcija → role/channel, lider → posebna rola).  
- **Dispatch**: poziva Discord modul i Database modul prema promjenama.  

---

## 3) Discord modul
**Svrha**  
Komunikacija s Discord API-jem.

**Glavne odgovornosti**  
- **Connection**: inicijalizacija bota, safe reconnect.  
- **Role management**: kreiranje/brisanje role, assign/remove za članove.  
- **Channel management**: kreiranje text/forum kanala, postavljanje permissions, rename/archive ako frakcija promijeni ime.  
- **Idempotent behaviour**: sigurno ponavljanje operacija.

---

## 4) Database (SQLite) modul
**Svrha**  
Persistencija podataka između restarta servera.

**Glavne odgovornosti**  
- **Schema**:
  - Mapiranje: FactionID → DiscordRoleID, FactionID → DiscordChannelID
  - Tablice:
    - `factions` (faction_id, tag, name, is_player_faction)
    - `players` (player_id, name, steam_id)
    - `faction_player` (player_id, faction_id)
    - `discord_roles` (faction_id, role_id)
    - `discord_channels` (faction_id, channel_id, channel_name)
- **Read / Write**: load mappings na startup, save nakon sync-a
- **Safety**: jedna connection instanca, lock oko write operacija

---

## 5) Security / Permissions modul
**Svrha**  
Zaštita od zlouporabe i kontrola tko smije upravljati pluginom.

**Glavne odgovornosti**
- SteamID whitelist (autorizirani administratori)
- Provjera prije kritičnih Torch komandi
- Opcionalno mapiranje Discord admin role
- Anti-abuse može biti isključen ako je Debug = true

---

## 6) Commands modul (Torch chat / console)
**Svrha**  
Administracija plugina bez restarta servera.

**Primjeri komandi**
- `/tds sync` → Force full resync
- `/tds cleanup` → Remove orphaned roles/channels
- `/tds status` → Show current sync state
- `/tds reload` → Reload config + database

---

## 7) Config modul
**Svrha**  
Centralizirana konfiguracija bez hardkodiranih vrijednosti.

**Sadržaj**
- Discord token
- Guild ID
- Sync interval
- Debug mode
- Security settings (SteamID whitelist)

---

## Database Models (C#)
```csharp
// FactionModel.cs
public class FactionModel
{
    public int FactionId { get; set; }
    public string Tag { get; set; }
    public string Name { get; set; }
    public bool IsPlayerFaction { get; set; }
    public List<ulong> MembersSteamIds { get; set; } = new List<ulong>();
}

// PlayerModel.cs
public class PlayerModel
{
    public long PlayerId { get; set; } // PlayerID in SE
    public string Name { get; set; }
    public ulong SteamId { get; set; }
}

// FactionPlayerModel.cs
public class FactionPlayerModel
{
    public long PlayerId { get; set; }
    public int? FactionId { get; set; }
}
```

## Završna napomena
Ovaj koncept:
- Strogo razdvaja odgovornosti
- Sprječava “spaghetti plugin”
- Torch modul ostaje stabilan
- Discord modul može se mijenjati neovisno
- Database modul ostaje jednostavan

➡️ Dugoročno održiv Torch plugin

