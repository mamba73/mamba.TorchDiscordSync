Torch Discord Sync - API Reference Summary
📚 API DISCOVERY SUMMARY
🎯 Death Detection Implementation Ready
Key Classes & Methods Found:

1. Player Management
File: Sandbox.Game.dll

[NS: Sandbox.Game.Multiplayer] -> Class: MyPlayerCollection
  - UInt64 TryGetSteamId(Int64 identityId)
  - MyPlayer TryGetPlayerBySteamId(UInt64 steamId, MyPlayer& player)
  - ICollection`1 GetOnlinePlayers()
  - MyPlayer GetPlayer(UInt64 steamId)
  - String TryGetIdentityNameFromSteamId(UInt64 steamId)
2. Death Event Detection
File: Sandbox.Game.dll

[NS: Sandbox.Game.Entities.Character] -> Class: MyCharacter
  - Void Kill(Boolean sync, MyDamageInformation damageInfo)
  - Void Die()
  - Boolean DoDamage(Single damage, MyStringHash damageType, Boolean updateSync, Int64 attackerId, Nullable`1 extraInfo)

[NS: Sandbox.Game.Components] -> Class: MyCharacterStatComponent
  - Void OnHealthChanged(Single newHealth, Single oldHealth, Object statChangeData)

[NS: Sandbox.Game.Components] -> Class: MyCharacterSoundComponent
  - Void OnCharacterDead()
3. Damage System
File: Sandbox.Game.dll

[NS: Sandbox.Game.Multiplayer] -> Class: MySyncDamage
  - Void DoDamageSynced(MyEntity entity, Single damage, MyStringHash type, Int64 attackerId, Nullable`1 extraInfo)
4. Mod API Interface
File: VRage.Game.dll

[NS: VRage.Game.ModAPI] -> Class: IMyCharacter
  - Void Kill(Object killData)
📋 Implementation Plan
Ready to implement death detection in PlayerTrackingService.cs by:

Subscribing to character death events
Capturing MyDamageInformation for death details
Integrating with existing DeathLogService
Sending notifications via EventLoggingService
Key Data Available:

✅ Player SteamID (TryGetSteamId)
✅ Player Name (TryGetIdentityNameFromSteamId)
✅ Death cause/damage type (MyDamageInformation)
✅ Attacker information (attackerId)
✅ Death location (via character position)
🚀 Next Steps
Extend PlayerTrackingService with death event subscription
Parse death information from MyDamageInformation
Connect with DeathLogService for processing
Send Discord notifications via EventLoggingService

This summary enables quick restart of death detection implementation