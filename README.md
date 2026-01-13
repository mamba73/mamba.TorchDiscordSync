# mamba.TorchDiscordSync

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)

Author: mamba  
Current Version: 0.1.0  
Torch Version: 1.3.1.328-master  
Space Engineers Version: 1.208.15

## Overview
This plugin synchronizes **Space Engineers player factions** with a Discord server.
It creates:
- Discord roles per faction
- Discord text/forums channels per faction
- Tracks faction → Discord mapping in a SQLite database

**Status:** 🚧 Initial Buildable Version

---

## File Structure

```
mamba.TorchDiscordSync/
├── Config/
│   └── PluginConfig.cs
├── Dependencies/
│   ├── NLog.dll
│   ├── Sandbox.Common.dll
│   ├── Sandbox.Game.dll
│   ├── Torch.API.dll
│   ├── Torch.dll
│   ├── VRage.dll
│   ├── VRage.Game.dll
│   ├── VRage.Library.dll
│   └── VRage.Math.dll
├── LICENSE
├── Models/
│   ├── FactionModel.cs
│   ├── FactionPlayerModel.cs
│   └── PlayerModel.cs
├── Plugin/
│   └── MambaTorchDiscordSyncPlugin.cs
├── README_ENG.md
├── README_HRV.md
├── Services/
│   ├── DatabaseService.cs
│   ├── DiscordService.cs
│   └── FactionSyncService.cs
├── Utils/
│   └── ChatUtils.cs
├── build.bat
├── manifest.xml
└── mamba.TorchDiscordSync.csproj
```


---

## Next Steps
- Implement faction parsing and SteamID mapping
- Sync roles/channels to Discord
- Track changes and update database
- Add commands for safe delete/undo
- Implement debug mode and logging

---

## How to Build
1. Open terminal in plugin root directory
2. Run:

```bash
./build.bat
```

## Dependencies

NLog.dll
Torch.API.dll
Torch.dll
Sandbox.Common.dll
Sandbox.Game.dll
VRage.Game.dll
VRage.Library.dll
VRage.Math.dll
VRage.dll


---