# mamba.TorchDiscordSync v2.0

---
**NOTE: This project is still in active development.** Many features are partially working or require fixes.  
Not everything listed below is fully stable yet – ongoing work.

**mamba.TorchDiscordSync** is a Torch plugin for Space Engineers that bridges the gap between your game server and your Discord community.

Unlike simple chat relays, this plugin focuses on **deep game integration**—tracking accurate kill data, analyzing damage sources in real-time, synchronizing player factions, and monitoring server health.

**Author**: mamba  
**Version**: 2.x.x  
**Torch**: 1.3.1+  
**Space Engineers**: 1.208+  
**C#**: 4.6+ / .NET Framework 4.8  

---
## 🚀 Project Status

| Feature | Status | Notes |
| :--- | :---: | :--- |
| **Core Plugin Structure** | ✅ Done | Service injection, Logging, Config handling. |
| **Smart Chat Relay** | ✅ Done | Bi-directional. Ignores private/faction messages. Loop protection. |
| **SimSpeed Monitor** | ✅ Done | Configurable watchdog (def. 30s) alerting admins on lag spikes. |
| **Advanced Killer Detection** | ✅ Done | Identifies weapons, turret owners, collisions, and environmental deaths. |
| **Damage Tracking Buffer** | ✅ Done | Real-time hooking into damage events. |
| **Discord Bot Connection** | ✅ Done | Basic bot connectivity and intent handling. |
| **Data Storage (XML)** | ✅ Done | Current storage solution. |
| **User Verification** | 🚧 In Progress | Securely linking SteamID to Discord UserID. |
| **Faction Synchronization** | 🚧 In Progress | Syncing SE Factions to Discord Roles. |
| **Secure Faction Chat** | ⏳ Planned | Private channels for faction comms (Game ↔ Discord). |
| **SQLite Migration** | ⏳ Planned | Critical for high-volume features (Raid Alerts). |

## 🌟 Key Features (Implemented)

### 💬 Smart Chat Relay
A robust bi-directional chat system that connects your server to a Discord channel.
- **Loop Protection:** Prevents bot messages from echoing back endlessly.
- **Privacy Filters:** Automatically ignores private DMs and internal Faction chat to protect sensitive gameplay info.
- **Bi-Directional:** Discord users can chat with in-game players seamlessly.

### 🛡️ Server Health Monitoring
- **SimSpeed Watchdog:** Automatically monitors server simulation speed. If SimSpeed drops below the threshold for a configurable time (default: 30s), it alerts administrators immediately.

### 🎯 Advanced Death Analysis
The plugin uses a sophisticated damage tracking system to analyze the exact cause of death:
- **PvP Detection:** Identifies the killer, weapon used, and grids involved.
- **Turret Tracking:** Traces automated turret fire back to the owner (even if offline).
- **Environmental Awareness:** Distinguishes between Collisions, Asphyxiation (LowPressure), and Gravity falls.

## 🗺️ Roadmap & Future Plans

### 1. SQLite Migration (High Priority)
To support high-frequency data logging (such as bullet impacts during heavy PvP) without causing server lag, the storage system will be migrated from XML to SQLite. This is a prerequisite for the Raid Alert system.

### 2. Raid Alerts (Offline Protection)
*Dependency: SQLite Migration*
A system to notify Faction members via Discord DM/Ping when their base is taking damage while they are offline. Requires the performance of SQLite to handle rapid damage events without impacting SimSpeed.

### 3. Secure Faction Chat
Once Faction Sync is complete, the plugin will create private Discord channels restricted to faction members, allowing secure communication between the game faction chat and Discord.

### 4. Leaderboards & Statistics
Weekly or monthly PvP rankings (K/D ratios, Most Active Faction, Top Ace Pilot) generated from the gathered kill data.

### 5. Bounty / Retaliation System
Building on the existing retaliation logic, this feature will allow players to place bounties on others, tracked and announced via Discord.

---
## Contributing
Pull requests are welcome! Please use C# 4.6+ compatibility.

## Support
No support yet!

---
*Project is currently under active development.*
*Developed by [mamba73](https://github.com/mamba73). Feel free to submit issues or pull requests!*

[Buy Me a Coffee ☕](https://buymeacoffee.com/mamba73)

