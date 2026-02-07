# 📋 DETAILED EXPLANATION: Complete Death Detection Procedure

## PART 1: THE TORCH APIS USED

### What Torch APIs are involved?

```csharp
1. MyAPIGateway.Session.DamageSystem
   - RegisterBeforeDamageHandler()
   - Called BEFORE damage is applied to any entity
   - Provides: DamageType, AttackerId, TargetId, Damage amount

2. MyEntities (VRage entity system)
   - OnEntityAdd / OnEntityRemove
   - Tracks when entities (grids, characters) are created/destroyed
   - Used to detect character respawns

3. IMyCharacter properties
   - StatComp (health, oxygen, energy stats)
   - ControllerInfo (which player controls this character)
   - EntityId (unique identifier for character)

4. MySession.Static
   - Players (player identities)
   - Factions (faction information)
   - Access to all game entities
```

---

## PART 2: COMPLETE FLOW - STEP BY STEP

### 🔴 SCENARIO: Player commits suicide

```
T0: Player takes off helmet / damages themselves
    ↓
    ┌──────────────────────────────────────────────────────────┐
T1: │ STAGE 1: DAMAGE SYSTEM HOOKS (BEFORE damage applied)     │
    │                                                           │
    │ Torch fires: DamageSystem.RegisterBeforeDamageHandler()  │
    │                                                           │
    │ TWO handlers are registered with DIFFERENT priorities:   │
    │                                                           │
    │ Priority 0 (fires FIRST):                                │
    │   → DamageTrackingService.OnDamageReceived()             │
    │   → Stores: VictimId, AttackerId, OwnerId, Timestamp     │
    │   → Buffer size: ~20 records per player                  │
    │   → Retention: 15 seconds auto-cleanup                   │
    │                                                           │
    │ Priority 1 (fires SECOND):                               │
    │   → KillerDetectionService.OnDamageReceived()            │
    │   → Stores: DamageType (string), AttackerId, Timestamp   │
    │   → Buffer size: 1 record per player                     │
    │   → Retention: 10 seconds auto-cleanup                   │
    │                                                           │
    │ MyDamageInformation object contains:                     │
    │   - Type: MyStringHash ("Suicide", "LowPressure", etc)   │
    │   - AttackerId: long (entity ID of attacker)             │
    │   - Target: object (victim entity - the character)       │
    │   - Amount: float (damage amount)                        │
    └──────────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T2: │ STAGE 2: CHARACTER DEATH EVENT (after damage applied)    │
    │                                                           │
    │ Torch fires: PlayerTrackingService.OnCharacterDied()     │
    │                                                           │
    │ This is hooked per-player by PlayerTrackingService       │
    │ Called when IMyCharacter health reaches 0                │
    │                                                           │
    │ Event data passed:                                       │
    │   - IMyCharacter: The dead character                     │
    │   - IHitInfo: Hit information (if available)             │
    │ └──────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T3: │ STAGE 3: KILLER DETECTION                                │
    │                                                           │
    │ Called from: PlayerTrackingService.OnPlayerDied()        │
    │ Method: DeathMessageHandler.HandlePlayerDeathAsync()     │
    │                                                           │
    │ Calls: KillerDetectionService.DetectKiller(character)    │
    │                                                           │
    │ STEP 1: Check DamageType local buffer                    │
    │         ├─ DamageType == "Suicide"? → DeathCause.Suicide│
    │         ├─ DamageType == "LowPressure"? → Oxygen death  │
    │         ├─ DamageType == "Fall"? → Fall death           │
    │         └─ DamageType == "Deformation"? → Collision     │
    │                                                           │
    │ STEP 2: Check DamageTracking buffer                      │
    │         ├─ Has recent attacker record?                  │
    │         ├─ AttackerId points to valid entity?           │
    │         └─ AnalyzeDamageDealer() → Get turret/player    │
    │                                                           │
    │ STEP 3: Try reflection on character                      │
    │         ├─ Look for m_lastDamageDealer field            │
    │         ├─ Look for m_lastAttacker field                │
    │         └─ Extract entity ID from field                 │
    │                                                           │
    │ STEP 4: Check oxygen stat (fallback)                     │
    │         ├─ character.StatComp.TryGetStat("oxygen")       │
    │         └─ If < 0.1f → Oxygen death                     │
    │                                                           │
    │ STEP 5: Environmental fallback                           │
    │         └─ If all else fails → Environment death        │
    │                                                           │
    │ Returns: KillerInfo object with:                         │
    │   - Cause: DeathCause (Suicide, Oxygen, Turret, etc)     │
    │   - KillerName: string (player/turret name)              │
    │   - WeaponName: string (turret type)                     │
    │   - IsPlayerKill: bool                                   │
    └──────────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T4: │ STAGE 4: LOCATION DETECTION                              │
    │                                                           │
    │ Called: DeathLocationService.DetectDeathZone(character)  │
    │                                                           │
    │ Returns: LocationZoneResult with:                        │
    │   - Zone: LocationZoneType (ON_SURFACE, IN_ORBIT, etc)   │
    │   - PlanetName: string                                   │
    │   - GridName: string                                     │
    │   - Coordinates: Vector3D                                │
    │                                                           │
    │ Uses: Vector3D.Distance() to calculate distance to       │
    │       planet center vs planet radius                     │
    └──────────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T5: │ STAGE 5: MESSAGE GENERATION                              │
    │                                                           │
    │ Called: GenerateDeathMessage(victim, killerInfo, location│
    │                                                           │
    │ Step 1: Map DeathCause to DeathTypeEnum                  │
    │         Suicide → DeathTypeEnum.Suicide                  │
    │                                                           │
    │ Step 2: Get random template from config                  │
    │         _deathMessagesConfig.GetRandomMessage()          │
    │         Returns: "{victim} took matters..."              │
    │                                                           │
    │ Step 3: Replace placeholders                             │
    │         {victim} → "mamba"                               │
    │         {killer} → "Self"                                │
    │         {weapon} → "Self-Inflicted"                      │
    │                                                           │
    │ Step 4: Add location                                     │
    │         GenerateLocationText() → "on Alien"              │
    │                                                           │
    │ Final message: "mamba took matters into their own hands  │
    │                 on Alien"                                │
    └──────────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T6: │ STAGE 6: SEND TO GAME CHAT                               │
    │                                                           │
    │ Method: SendToGameChat(message)                          │
    │                                                           │
    │ Torch API: MyVisualScriptLogicProvider.SendChatMessage() │
    │                                                           │
    │ Displays in-game: [Server] "mamba took matters..."       │
    └──────────────────────────────────────────────────────────┘
    ↓
    ┌──────────────────────────────────────────────────────────┐
T7: │ STAGE 7: SEND TO DISCORD                                 │
    │                                                           │
    │ Method: SendToDiscordAsync(message)                      │
    │                                                           │
    │ Service: EventLoggingService.LogDeathAsync()             │
    │                                                           │
    │ Sends async HTTP request to Discord webhook              │
    │                                                           │
    │ Discord displays: "💀 mamba took matters..."             │
    └──────────────────────────────────────────────────────────┘
```

---

## PART 3: WHERE EACH METHOD COMES FROM

### DamageSystem Hook
```csharp
// File: Services/DamageTrackingService.cs

public void Init()
{
    // Torch API: Register damage handler with DamageSystem
    MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(
        priority: 0,  // Fire first (0 = first, higher = later)
        handler: OnDamageReceived
    );
}

// Called by Torch engine BEFORE any damage is applied
private void OnDamageReceived(object target, ref MyDamageInformation info)
{
    // info.Type.String = "Suicide", "LowPressure", "Fall", etc.
    // info.AttackerId = entity ID of attacker
    // target = victim (IMyCharacter)
}
```

### Character Death Hook
```csharp
// File: Services/PlayerTrackingService.cs

public void HookPlayerDeath(IMyCharacter character)
{
    // Hook the character's death event
    // Note: This is custom hook, not standard Torch API
    
    // When character health reaches 0:
    character.OnClosing += (entity) =>
    {
        if (entity is IMyCharacter deadChar)
        {
            OnCharacterDied(deadChar);
        }
    };
}

private void OnCharacterDied(IMyCharacter character)
{
    // CHARACTER IS DEAD - NOW ANALYZE
    _deathMessageHandler.HandlePlayerDeathAsync(playerName, character);
}
```

### Killer Detection
```csharp
// File: Services/KillerDetectionService.cs

public KillerInfo DetectKiller(IMyCharacter victim)
{
    // STEP 1: Check DamageType from local buffer
    if (_localDamageBuffer.TryGetValue(victim.EntityId, out var record))
    {
        if (record.DamageType == "Suicide")
        {
            return new KillerInfo 
            { 
                Cause = DeathCause.Suicide,
                KillerName = "Self",
                WeaponName = "Self-Inflicted"
            };
        }
    }

    // STEP 2: Check DamageTracking buffer
    var lastDamage = damageTracking.GetLastDamage(victim.EntityId, 5);
    if (lastDamage != null)
    {
        MyEntity attacker;
        if (MyEntities.TryGetEntityById(lastDamage.AttackerId, out attacker))
        {
            // Analyze what type of entity killed the player
            var turret = attacker as IMyLargeTurretBase;
            var character = attacker as IMyCharacter;
            var grid = attacker as MyCubeGrid;
            
            if (turret != null)
                return AnalyzeTurret(turret);
            if (character != null)
                return AnalyzeCharacter(character);
            if (grid != null)
                return AnalyzeGrid(grid);
        }
    }

    // STEP 3: Try reflection
    long dealerId = GetLastDamageDealerId(victim);
    // ... search by ID ...

    // STEP 4: Check oxygen stat
    // ... check character.StatComp ...

    // STEP 5: Environmental fallback
    return new KillerInfo { Cause = DeathCause.Environment };
}
```

### Getting Attacker Information
```csharp
// How do we find WHO attacked?

// METHOD 1: From DamageTracking buffer (MOST RELIABLE)
var lastDamage = damageTracking.GetLastDamage(victimId, 5);
// Contains:
//   - AttackerId (entity ID)
//   - OwnerId (block owner)
//   - OwnerName (player name)
//   - DamageAmount
//   - Timestamp

// METHOD 2: From DamageType local buffer
var record = _localDamageBuffer[victimId];
// Contains:
//   - DamageType ("Suicide", "Fall", "LowPressure")
//   - AttackerId
//   - Timestamp

// METHOD 3: Reflection (if entity still exists)
FieldInfo field = typeof(MyCharacter).GetField(
    "m_lastDamageDealer",
    BindingFlags.NonPublic | BindingFlags.Instance
);
long dealerId = (long)field.GetValue(victim);
// Then: MyEntities.TryGetEntityById(dealerId, out entity)

// METHOD 4: From entity system
MyEntities.TryGetEntityById(entityId, out MyEntity entity);
if (entity is IMyLargeTurretBase turret)
{
    var owner = MySession.Static.Players.TryGetIdentity(
        turret.CubeGrid.GetOwner()
    );
}
```

---

## PART 4: DATA FLOW DIAGRAM

```
DamageSystem.OnDamageReceived()
    ↓
    ├─ DamageTrackingService captures: [VictimId, AttackerId, OwnerId, Name]
    │  Retention: 15 seconds, ~20 per player
    │
    └─ KillerDetectionService captures: [DamageType, AttackerId, Timestamp]
       Retention: 10 seconds, 1 per player

Character.OnClosing()
    ↓
    PlayerTrackingService.OnPlayerDied()
    ↓
    DeathMessageHandler.HandlePlayerDeathAsync()
    ↓
    KillerDetectionService.DetectKiller()
        ├─ Check: DamageType buffer
        ├─ Check: DamageTracking buffer
        ├─ Check: Reflection
        ├─ Check: Oxygen stat
        └─ Check: Environmental fallback
    ↓
    Returns: KillerInfo { Cause, KillerName, WeaponName }
    ↓
    DeathLocationService.DetectDeathZone()
    ↓
    Returns: LocationZoneResult { Zone, Planet, Grid }
    ↓
    GenerateDeathMessage(victim, killer, location)
    ↓
    SendToGameChat() + SendToDiscordAsync()
```

---

## PART 5: KEY CLASSES AND THEIR ROLES

```csharp
// REGISTRATION CLASSES
DamageTrackingService
  ├─ Init() → Registers BeforeDamageHandler (Priority 0)
  ├─ OnDamageReceived() → Stores complete damage info
  └─ GetLastDamage(victimId, seconds) → Retrieve from buffer

KillerDetectionService
  ├─ Init() → Registers BeforeDamageHandler (Priority 1)
  ├─ OnDamageReceived() → Stores DamageType
  ├─ DetectKiller(character) → Multi-step detection
  └─ Cleanup() → Remove old records

// EVENT HANDLING
PlayerTrackingService
  ├─ Initialize() → Hooks CharacterDied event per player
  └─ OnCharacterDied() → Calls DeathMessageHandler

// MESSAGE GENERATION
DeathMessageHandler
  ├─ HandlePlayerDeathAsync() → Orchestrates entire flow
  ├─ GenerateDeathMessage() → Creates message from template
  └─ SendToGameChat() + SendToDiscordAsync() → Delivery

DeathLocationService
  └─ DetectDeathZone() → Calculates location context

// CONFIGURATION
DeathMessagesConfig
  └─ GetRandomMessage(DeathTypeEnum) → Selects from config
```

---

## PART 6: HOW TO EXTRACT KILLER INFORMATION

### The Three Buffers

**Buffer 1: DamageTrackingService** (Most Complete)
```csharp
public class DamageRecord
{
    public long VictimId { get; set; }
    public long AttackerId { get; set; }        // ← Entity ID
    public long OwnerId { get; set; }           // ← Block owner
    public string OwnerName { get; set; }       // ← Player name
    public string FactionTag { get; set; }      // ← NPC faction
    public float DamageAmount { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Buffer 2: KillerDetectionService** (DamageType Info)
```csharp
private class DamageRecord
{
    public string DamageType { get; set; }      // ← "Suicide", "Fall", etc.
    public long AttackerId { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Buffer 3: Reflection** (Last Resort)
```csharp
// Torch stores m_lastDamageDealer in character
// But Space Engineers often clears this after damage
// Used as fallback when buffers don't have data
```

---

## PART 7: TORCH API REFERENCE

### Damage System
```csharp
MyAPIGateway.Session.DamageSystem
├─ RegisterBeforeDamageHandler(priority, handler)
│  Signature: void handler(object target, ref MyDamageInformation info)
│  target: The victim entity (usually IMyCharacter)
│  info: Contains Type, AttackerId, DamageAmount
│
└─ DamageType string values:
   - "Suicide" (self-inflicted)
   - "LowPressure" (vacuum/oxygen)
   - "Asphyxia" (no air)
   - "Fall" (gravity/impact)
   - "Deformation" (collision)
   - "Heat" (fire/extreme temperature)
   - "Explosion" (bomb/detonation)
   - etc.
```

### Entity System
```csharp
MyEntities
├─ TryGetEntityById(long id, out MyEntity entity)
├─ OnEntityAdd += handler
├─ OnEntityRemove += handler
└─ Get() → Get all entities

MySession.Static
├─ Players → Player identities
├─ Factions → All factions
├─ GetEntityById(id) → Direct access
└─ Entities → All game entities
```

### Character Properties
```csharp
IMyCharacter
├─ DisplayName → Player name
├─ EntityId → Unique entity ID
├─ ControllerInfo.ControllingIdentityId → Player ID
├─ StatComp → Health/oxygen/energy stats
│  ├─ TryGetStat(hash, out MyEntityStat)
│  └─ Stats[hash].Value → Current value
└─ OnClosing → Event when character dies
```

REPORT: G:\dev\SE\csharp\mamba.TorchDiscordSync\Dependencies
SEARCH: MyDamageType | FILTER: None
========================================

FILE: VRage.Game.dll (v1.0.0.0)
========================================

[NS: VRage.Game] -> Class: MyDamageType
  [F] [ST] MyStringHash Unknown
  [F] [ST] MyStringHash Explosion
  [F] [ST] MyStringHash Rocket
  [F] [ST] MyStringHash Bullet
  [F] [ST] MyStringHash Mine
  [F] [ST] MyStringHash Environment
  [F] [ST] MyStringHash Thruster
  [F] [ST] MyStringHash Drill
  [F] [ST] MyStringHash Radioactivity
  [F] [ST] MyStringHash Deformation
  [F] [ST] MyStringHash Suicide
  [F] [ST] MyStringHash Fall
  [F] [ST] MyStringHash Weapon
  [F] [ST] MyStringHash Fire
  [F] [ST] MyStringHash Squeez
  [F] [ST] MyStringHash Grind
  [F] [ST] MyStringHash Weld
  [F] [ST] MyStringHash Asphyxia
  [F] [ST] MyStringHash LowPressure
  [F] [ST] MyStringHash Bolt
  [F] [ST] MyStringHash Destruction
  [F] [ST] MyStringHash Debug
  [F] [ST] MyStringHash Wolf
  [F] [ST] MyStringHash Spider
  [F] [ST] MyStringHash Temperature
  [F] [ST] MyStringHash OutOfBounds
  [F] [ST] MyStringHash Hunger
  [F] [ST] MyStringHash Weather
  - bool Equals(Object obj)
  - [ST] bool Equals(Object objA, Object objB)
  - [ST] bool ReferenceEquals(Object objA, Object objB)
  - int GetHashCode()
  - Type GetType()
  - string ToString()
