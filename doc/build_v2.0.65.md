# mamba.TorchDiscordSync - README & TODO

## 📋 PROJECT OVERVIEW
Advanced Space Engineers Torch plugin for Discord synchronization with death logging, chat sync, server monitoring, and admin commands.

---

## ✅ COMPLETED FEATURES (Implemented & Working)

### 🔥 CORE STABILITY
- **[DONE]** Server START/STOP notifications to Discord
- **[DONE]** Player JOIN/LEAVE detection and notifications  
- **[DONE]** Online player count tracking
- **[DONE]** Real-time SimSpeed calculation (not hardcoded 1.0)
- **[DONE]** Configuration-driven messages (from MainConfig.xml)

### 🛠️ ARCHITECTURE
- **[DONE]** Modular design with separate services
- **[DONE]** PlayerTrackingService for join/leave detection
- **[DONE]** EventLoggingService for Discord communication
- **[DONE]** Proper session state handling
- **[DONE]** Fallback mechanisms (chat receiver + polling)

---

## 🎯 CURRENTLY IMPLEMENTED CLASSES & FILES

### **Plugin/MambaTorchDiscordSyncPlugin.cs**
- Main plugin entry point
- Session state management (Loading/Loaded/Unloading/Unloaded)
- Service initialization and orchestration
- SimSpeed calculation and reporting

### **Services/PlayerTrackingService.cs**
- Instant player detection via ChatManagerClient.Attach()
- Fallback polling mechanism (5-second intervals)
- System message parsing for join/leave events
- Proper resource cleanup

### **Services/EventLoggingService.cs**
- Server status logging (STARTED/STOPPED with real SimSpeed)
- Player join/leave logging
- Death logging (prepared for implementation)
- Discord message sending via DiscordService wrapper

### **Handlers/EventManager.cs**
- Server startup/shutdown message sending
- Utility methods maintained (death message building)
- Problematic event handlers commented out (non-existent APIs)

### **Config/MainConfig.cs**
- All messages configurable via XML
- Monitoring section for server status
- Chat section for join/leave messages

---

## 🔨 TODO LIST - PHASED DEVELOPMENT

### 🔥 PHASE 1 - PRIORITY FEATURES (Next to implement)

#### **1. Death Detection System**
**Class:** `Services/PlayerTrackingService.cs` (needs extension)
**Method:** Add `CharacterDied` event handler
**Integration:** Connect with existing `DeathLogService`
**Features:**
- Player vs Player deaths
- Suicide detection  
- Environmental deaths (oxygen, pressure, etc.)
- Grid/entity kills
- Detailed death messages with weapons/types

#### **2. SteamID Display Option**
**Class:** `Config/MainConfig.cs` (add new setting)
**Class:** `Services/EventLoggingService.cs` (modify logging methods)
**Setting:** `ShowSteamIDInChat = true/false`
**Impact:** Player notifications will include SteamID when enabled

#### **3. Chat Synchronization** 
**Class:** `Services/ChatSyncService.cs` (enhance existing)
**Features:**
- Game → Discord chat mirroring
- Discord → Game chat mirroring  
- Faction-based channel routing
- Message sanitization and filtering

### 🚀 PHASE 2 - ENHANCEMENTS

#### **4. Advanced Death Logging**
**Class:** `Services/DeathLogService.cs` (enhance)
**Features:**
- Retaliation detection system
- Kill streak tracking
- Death statistics and analytics
- Database persistence improvements

#### **5. Faction Management Improvements**
**Class:** `Services/FactionSyncService.cs` (move logic from plugin)
**Class:** `Core/SyncOrchestrator.cs` (coordinate)
**Features:**
- Real faction data loading (remove test data)
- Automatic Discord role/channel creation
- Faction member synchronization
- Leave/join notifications

#### **6. Chat Moderation System**
**Class:** `Handlers/ChatModerator.cs` (fix warnings)
**Features:**
- Blacklisted word filtering
- Attachment/link blocking
- Warning/mute/kick system
- Admin logging channel integration

### 💎 PHASE 3 - ADVANCED FEATURES

#### **7. Database Migration to SQLite**
**Class:** `Services/DatabaseService.cs` (major refactor)
**Migration:** XML → SQLite
**Benefits:** Better performance, queries, relationships

#### **8. Enhanced Verification System**
**Class:** `Services/VerificationService.cs` (extend)
**Features:**
- Multi-step verification process
- Temporary codes with expiration
- Discord role assignment automation
- Web panel integration possibility

#### **9. Performance Monitoring**
**Class:** `Services/MonitoringService.cs` (new)
**Features:**
- Memory/CPU usage tracking
- Player count history
- SimSpeed trend analysis
- Alert system for issues

---

## 🏗️ TECHNICAL ROADMAP

### **Immediate Next Steps:**
1. **Extend PlayerTrackingService** - Add death event subscription
2. **Update MainConfig** - Add ShowSteamIDInChat option  
3. **Enhance DeathLogService** - Connect with player tracking
4. **Fix async warnings** - Address CS4014 warnings in existing code

### **Class Interaction Vision:**

MambaTorchDiscordSyncPlugin (Orchestrator)
 ├── PlayerTrackingService (Player/Death Events)
 │ ├── EventLoggingService (Discord Notifications)
 │ └── DeathLogService (Death Processing)
 ├── ChatSyncService (Chat Mirroring)
 ├── FactionSyncService (Faction Management)
 ├── VerificationService (User Verification)
 └── CommandProcessor (Command Handling)


### **Data Flow:**
1. **Player Event** → PlayerTrackingService → EventLoggingService → Discord
2. **Death Event** → PlayerTrackingService → DeathLogService → EventLoggingService → Discord  
3. **Chat Message** → ChatSyncService → DiscordService → Discord
4. **Commands** → CommandProcessor → Various Services

---

## ⚙️ CONFIGURATION IMPROVEMENTS PLANNED

### **New Settings to Add:**
```xml
<Chat>
  <ShowSteamIDInChat>true</ShowSteamIDInChat>
  <DeathNotificationsEnabled>true</DeathNotificationsEnabled>
  <DeathChannelId>123456789</DeathChannelId>
</Chat>

<DeathLogging>
  <Enabled>true</Enabled>
  <IncludeWeaponInfo>true</IncludeWeaponInfo>
  <IncludeLocation>true</IncludeLocation>
  <RetaliationTracking>true</RetaliationTracking>
</DeathLogging>
```


## 📊 STATUS SUMMARY

Feature	Status	Notes
Server UP/DOWN	✅ DONE	Working with real SimSpeed
Player JOIN/LEAVE	✅ DONE	Instant + polling backup
Death Detection	⏳ TODO	Next priority
Chat Sync	⏳ TODO	Partially exists
SteamID Option	⏳ TODO	Easy config addition
Verification	✅ EXISTS	Needs enhancement
Faction Sync	⏳ WIP	Test data only

Last Updated: January 28, 2026


## 🎯 NEXT IMMEDIATE TASKS:

1. **Add death detection** to PlayerTrackingService
2. **Add ShowSteamIDInChat** config option  
3. **Fix async warnings** in existing codebase
4. **Move faction logic** from plugin to FactionSyncService

Would you like me to start implementing any of these next features?


