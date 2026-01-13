# Project Idea: mamba.TorchDiscordSync

## 1) Torch / Space Engineers module
**Purpose:** interact with SE server via Torch API.

**Responsibilities:**
- Session lifecycle
    - Hook on ITorchSessionManager.SessionStateChanged
    - React only on TorchSessionState.Loaded
- Faction reader
    - Fetch all factions from SE world
    - Read: Faction ID, Tag, Name, Leader SteamID, Members SteamID
- Change detection
    - Periodic scan (e.g., every 60s)
    - Detect new/deleted factions, member changes, leader changes
- Output: normalized `FactionModel` object

## 2) Core Sync / Orchestration module
**Purpose:** central brain, connects data, does not call Torch or Discord directly.

**Responsibilities:**
- State management
    - Keeps last known faction state
    - Decides what changed
- Rules engine
    - Every faction → Discord role
    - Every faction → Discord channel
    - Nickname: `[TAG] OriginalNick`
- Dispatch
    - Actions → Discord module
    - Changes → Database module

## 3) Discord module
**Purpose:** communicate with Discord API.

**Responsibilities:**
- Connection: bot initialization, safe reconnect
- Role management: create/delete roles
- Channel management: create/delete channels, set permissions
- Nickname sync: `[TAG] OriginalNick`, undo / rollback
- Idempotent behaviour: safe repeated operations

## 4) Database (SQLite) module
**Purpose:** persist data.

**Responsibilities:**
- Schema: FactionID → DiscordRoleID, FactionID → DiscordChannelID, Player nick mapping
- Read / Write: load on startup, save after sync
- Soft delete fields: `DeletedAt` for undo
- Single SQLite connection, thread-safe

## 5) Security / Permissions module
- SteamID whitelist
- Check before executing commands (Torch / Discord)
- Anti-abuse active only in production (Debug = false)

## 6) Commands module (Torch chat / console)
- `/tds sync` – force full resync
- `/tds cleanup` – remove orphaned roles/channels
- `/tds status` – show current sync state
- `/tds reload` – reload config + database

## 7) Config module
- Discord token
- Guild ID
- Sync interval
- Debug mode
- Security settings (SteamID whitelist)

---

**Note:**  
All changes (nicknames, role/channel, timestamps) are logged.  
Database stores only state for undo / rollback.  
Discord nickname format: `[TAG] OriginalNick`.
