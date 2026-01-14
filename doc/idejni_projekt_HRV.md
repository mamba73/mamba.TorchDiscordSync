# idejni_projekt_HRV.md

# Idejni koncept plugina mamba.TorchDiscordSync

## 1) Torch / Space Engineers modul
**Svrha:** Komunicira sa SE serverom preko Torch API.

**Glavne odgovornosti:**
- Session lifecycle: hook na `ITorchSessionManager.SessionStateChanged`, reagira samo na `TorchSessionState.Loaded`
- Čitanje fakcija: ID, tag, ime, članovi (SteamID)
- Promjene fakcija: periodički scan, detekcija novih/promijenjenih/obrisanih fakcija
- Output: normalizirani `FactionModel` objekt za Core Sync

## 2) Core Sync / Orchestration modul
**Svrha:** Centralni mozak plugina, povezuje podatke.

**Glavne odgovornosti:**
- State management: prati zadnje stanje fakcija
- Rules engine: provjerava pravila (Discord role, channel)
- Dispatch: poziva Discord modul i Database modul, nikada direktno Torch API

## 3) Discord modul
**Svrha:** Komunikacija s Discord API.

**Glavne odgovornosti:**
- Connection: inicijalizacija bota, safe reconnect
- Role management: kreiranje / brisanje role za članove fakcije
- Channel management: kreiranje text/forum channela, permissions
- Nick sync: `[TAG] originalNick` format
- Idempotent behavior: ako već postoji → ne radi ništa

## 4) Database (SQLite) modul
**Svrha:** Persistencija podataka između restarta servera.

**Glavne odgovornosti:**
- Schema: FactionID → DiscordRoleID, FactionID → DiscordChannelID, Player SteamID, original nick
- Read/Write: load na startup, save nakon svake sinkronizacije
- Safety: jedna connection instanca, lock oko write operacija

## 5) Security / Permissions modul
**Svrha:** Zaštita od zlouporabe.

**Glavne odgovornosti:**
- SteamID whitelist
- Discord admin role (opcionalno)
- Provjera prije izvršavanja komandi

## 6) Commands modul (Torch chat / console)
**Svrha:** Admin kontrola plugina bez restarta.

**Primjeri komandi:**
- /tds sync → force full resync
- /tds cleanup → remove orphaned roles/channels
- /tds status → show current sync state
- /tds reload → reload config + database

## 7) Config modul
**Svrha:** Centralizirana konfiguracija (XML).

**Sadržaj:**
- Discord token, Guild ID
- Sync interval, Debug mode
- Security settings (SteamID whitelist)

**Napomena:**  
Torch dio ostaje stabilan, Discord dio može se mijenjati, database trivijalan, plug-in dugoročno održiv.
