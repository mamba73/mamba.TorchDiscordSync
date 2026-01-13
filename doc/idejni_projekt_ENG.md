# mamba.TorchDiscordSync

Author: mamba  
Current Version: 0.1.0  
Torch Version: 1.3.1.328-master  
Space Engineers Version: 1.208.15

## Overview
This plugin synchronizes **Space Engineers player factions** with a Discord server.
It provides:
- Discord roles per faction
- Discord text/forum channels per faction
- Tracks faction → Discord mapping in SQLite

**Status:** 🚧 Initial buildable skeleton

---

## 1) Torch / Space Engineers module
**Purpose**  
Handles all interactions with the SE server via Torch API.

**Key responsibilities**  
- **Session lifecycle**: hook into `ITorchSessionManager.SessionStateChanged`, react only to `TorchSessionState.Loaded`.  
- **Faction reader**: read all factions, tag, name, leader, members (SteamID).  
- **Change detection**: periodic scan, compare with previous snapshot, detect new/deleted factions, membership changes, leader changes.  
- **Output**: `FactionModel` object → Core Sync Layer.

---

## 2) Core Sync / Orchestration module
**Purpose**  
Central brain of the plugin, knows nothing about Torch or Discord.

**Key responsibilities**  
- **State management**: stores last known faction state, decides what has changed.  
- **Rules engine**: rules like (each faction → role/channel, leader → special role).  
- **Dispatch**: calls Discord module and Database module based on detected changes.  

---

## 3) Discord module
**Purpose**  
Handles all Discord API communication.

**Key responsibilities**  
- **Connection**: initialize bot, safe reconnect.  
- **Role management**: create/delete role, assign/remove for members.  
- **Channel management**: create text/forum channels, set permissions, rename/archive if faction changes.  
- **Idempotent behaviour**: safe to repeat operations.

---

## 4) Database (SQLite) module
**Purpose**  
Persist data across server restarts.

**Key responsibilities**  
- **Schema**:
  - Map: FactionID → DiscordRoleID, FactionID → DiscordChannelID
  - Tables:
    - `factions` (faction_id, tag, name, is_player_faction)
    - `players` (player_id, name, steam_id)
    - `faction_player` (player_id, faction_id)
    - `discord_roles` (faction_id, role_id)
    - `discord_channels` (faction_id, channel_id, channel_name)
- **Read / Write**: load mappings at startup, save after each sync
- **Safety**: single connection instance, lock around write operations

---

## 5) Security / Permissions module
**Purpose**  
Prevent abuse and control who can manage the plugin.

**Key responsibilities**
- SteamID whitelist (authorized admins)
- Validate before critical Torch commands
- Optional Discord admin role mapping
- Anti-abuse can be disabled if Debug = true

---

## 6) Commands module (Torch chat / console)
**Purpose**  
Manage the plugin without server restart.

**Example commands**
- `/tds sync` → Force full resync
- `/tds cleanup` → Remove orphaned roles/channels
- `/tds status` → Show current sync state
- `/tds reload` → Reload config + database

---

## 7) Config module
**Purpose**  
Centralized configuration without hardcoding.

**Content**
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

## Final Note
This design:
- Strictly separates responsibilities
- Prevents “spaghetti plugin”
- Torch module stays stable
- Discord module can evolve independently
- Database remains simple

➡️ Long-term maintainable Torch plugin

