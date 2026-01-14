# mamba.TorchDiscordSync Plugin for Space Engineers (Torch Server)

Author: mamba  
Version: 1.0.0  
Torch Version: 1.3.1.328-master  
Space Engineers Version: 1.208.15

## Overview
This plugin synchronizes Space Engineers factions and player nicknames with Discord, automatically prefixing nicknames with faction tags and keeping everything logged. All members in a faction get the same Discord permissions. Leaders do not get special Discord roles.  

## Features
- Faction and player mapping
- `[TAG] originalNick` nickname sync
- Discord role and channel creation for factions
- SQLite database for persistent mapping
- Configurable via XML (PluginConfig.xml)
- Logging of changes and sync operations
- Commands for force sync, cleanup, and status
- Compatible with C# 4.6 and .NET Framework 4.8

## File Structure
```
mamba.TorchDiscordSync/
├─ Config/
│  └─ PluginConfig.cs
├─ data/
│  └─ MambaTorchDiscordSync.cfg
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
├─ doc/
│  ├─ idejni_projekt_HRV.md
│  └─ project_idea_ENG.md
├── Instance/
│   └── mambaTorchDiscordSync/
│       └── MambaTorchDiscordSyncData.xml      ← save data
├─ Models/
│  ├─ FactionModel.cs
│  ├─ FactionPlayerModel.cs
│  ├─ PlayerModel.cs
│  └─ RootDataModel.cs
├─ Plugin/
│  └─ MambaTorchDiscordSyncPlugin.cs
├─ Services/
│  ├─ DatabaseService.cs
│  ├─ DiscordService.cs
│  └─ FactionSyncService.cs
├─ Utils/
│  └─ ChatUtils.cs
├─ .gitignore
├─ build.bat
├─ LICENSE
├─ mamba.TorchDiscordSync.csproj
├─ mamba.TorchDiscordSync.sln
├─ manifest.xml
├─ README.md
└─ tree
```
