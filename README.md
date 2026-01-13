# mamba.TorchDiscordSync

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)

Author: mamba  
Current Version: 0.1.0  
Torch Version: 1.3.1.328-master  
Space Engineers Version: 1.208.15

## Overview
This plugin synchronizes **Space Engineers player factions** with a Discord server.

Key Features:
- Creates Discord roles per player faction
- Creates private text/forum channels per faction
- Synchronizes player nicknames: `[TAG] OriginalNick`
- Tracks changes safely in a local SQLite database
- Logging all changes and events

**Status:** 🚧 Initial Buildable Version

---

## Build & Installation

1. Clone the repository:

    git clone git@github.com:mamba73/mamba.TorchDiscordSync.git
    cd mamba.TorchDiscordSync

2. Make sure you have the required **Dependencies/** DLLs for Torch server.

3. Build the plugin:
```bash
    ./build.bat
```

4. After build, the DLL will be in `bin/Release/OfflineStaticProtection.dll`.  
   The zip `OfflineStaticProtection.zip` can be uploaded to your Torch plugins folder.

---

## Configuration
Configure in `Config/PluginConfig.cs` or via config JSON once implemented.

Key options:
- Discord token
- Guild ID
- Sync interval
- Debug mode
- Security / admin SteamID whitelist

---

## Database
SQLite stores:
- Current faction-role-channel mapping
- Player original & synced nicknames
- Timestamps for created_at / updated_at / soft delete (deleted_at)

Event logs (player join/leave, nickname changes, Discord role/channel creation) are **only written to log files**, not database.

---

## Future Features
- Multi-server support
- Auto-cleanup of unused roles/channels
- Undo / rollback of nickname and Discord object changes
- Extended security (SteamID + Discord admin whitelist)
