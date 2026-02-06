// Services/KillerDetectionService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
// FIX: Added this namespace to resolve MyEntityStat and access StatComp methods
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// CORRECT: Killer detection using damage system reflection
    /// Uses MyEntityStat and character damage tracking to find actual killer
    /// No proximity detection - finds REAL damage source!
    /// </summary>
    public class KillerDetectionService
    {
        /// <summary>
        /// Result of killer detection
        /// </summary>
        public class KillerInfo
        {
            public bool IsPlayerKill { get; set; }
            public string KillerName { get; set; }
            public string WeaponName { get; set; }
            public bool IsTurretKill { get; set; }
            public string TurretOwnerName { get; set; }
            public bool IsNpcFaction { get; set; }
            public string NpcFactionTag { get; set; }
            public DeathCause Cause { get; set; }

            public KillerInfo()
            {
                IsPlayerKill = false;
                KillerName = "Unknown";
                WeaponName = "Unknown";
                IsTurretKill = false;
                TurretOwnerName = null;
                IsNpcFaction = false;
                NpcFactionTag = null;
                Cause = DeathCause.Unknown;
            }
        }

        public enum DeathCause
        {
            Unknown,
            Player,
            Turret,
            NpcFaction,
            Collision,
            Fall,
            Oxygen,
            Pressure,
            Suicide,
            Environment,
        }

        /// <summary>
        /// CORRECT: Detect killer using character's last damage source
        /// Uses reflection to access internal damage tracking
        /// </summary>
        public KillerInfo DetectKiller(IMyCharacter victim)
        {
            var info = new KillerInfo();

            try
            {
                if (victim == null)
                {
                    LoggerUtil.LogDebug("[KILLER] Victim is null");
                    return info;
                }

                LoggerUtil.LogDebug($"[KILLER] ═══ Detecting killer for {victim.DisplayName} ═══");

                var myCharacter = victim as MyCharacter;
                if (myCharacter == null)
                {
                    LoggerUtil.LogDebug("[KILLER] Cannot cast to MyCharacter");
                    return info;
                }

                // STEP 1: Check oxygen depletion first (easy to detect)
                if (CheckOxygenDeath(myCharacter, info))
                {
                    LoggerUtil.LogSuccess("[KILLER] Death by oxygen depletion");
                    return info;
                }

                // STEP 2: Try to get last damage dealer using reflection
                long lastDamageDealerId = GetLastDamageDealerId(myCharacter);

                if (lastDamageDealerId != 0)
                {
                    LoggerUtil.LogDebug($"[KILLER] Last damage dealer ID: {lastDamageDealerId}");

                    MyEntity damageDealer;
                    if (MyEntities.TryGetEntityById(lastDamageDealerId, out damageDealer))
                    {
                        LoggerUtil.LogDebug(
                            $"[KILLER] Damage dealer: {damageDealer.GetType().Name} - {damageDealer.DisplayName}"
                        );
                        AnalyzeDamageDealer(damageDealer, info);
                        return info;
                    }
                    else
                    {
                        LoggerUtil.LogWarning(
                            $"[KILLER] Damage dealer entity {lastDamageDealerId} not found"
                        );
                    }
                }

                // STEP 3: Try alternative methods
                if (TryGetKillerFromStatComp(myCharacter, info))
                {
                    LoggerUtil.LogSuccess($"[KILLER] Found via StatComp");
                    return info;
                }

                // STEP 4: Check environmental causes
                DetectEnvironmentalDeath(myCharacter, info);

                LoggerUtil.LogDebug(
                    $"[KILLER] Final: Cause={info.Cause}, Killer={info.KillerName}, Weapon={info.WeaponName}"
                );
                LoggerUtil.LogDebug($"[KILLER] ═══════════════════════════════════════");

                return info;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER] Error: {ex.Message}\n{ex.StackTrace}");
                return info;
            }
        }

        private long GetLastDamageDealerId(MyCharacter character)
        {
            try
            {
                var characterType = character.GetType();
                string[] possibleFieldNames = new string[]
                {
                    "m_lastDamageDealer",
                    "m_lastAttacker",
                    "m_lastDamageSource",
                    "LastDamageDealer",
                    "LastAttacker",
                };

                foreach (var fieldName in possibleFieldNames)
                {
                    var field = characterType.GetField(
                        fieldName,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                    if (field != null)
                    {
                        var value = field.GetValue(character);
                        LoggerUtil.LogDebug($"[KILLER_REFLECT] Found field '{fieldName}': {value}");

                        if (value is long)
                            return (long)value;
                        else if (value is MyEntity)
                            return ((MyEntity)value).EntityId;
                    }
                }

                LoggerUtil.LogDebug("[KILLER_REFLECT] No damage dealer field found via reflection");
                return 0;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_REFLECT] Error: {ex.Message}");
                return 0;
            }
        }

        private bool TryGetKillerFromStatComp(MyCharacter character, KillerInfo info)
        {
            try
            {
                if (character.StatComp == null)
                    return false;

                var statCompType = character.StatComp.GetType();
                LoggerUtil.LogDebug($"[KILLER_STAT] StatComp type: {statCompType.Name}");

                // FIX: character.StatComp.Stats is a DictionaryValuesReader which doesn't have GetStat().
                // We must use TryGetStat() directly on the StatComp (MyEntityStatComponent).
                MyEntityStat healthStat;
                if (
                    character.StatComp.TryGetStat(
                        MyStringHash.GetOrCompute("health"),
                        out healthStat
                    )
                )
                {
                    LoggerUtil.LogDebug(
                        $"[KILLER_STAT] Health: {healthStat.Value}/{healthStat.MaxValue}"
                    );

                    var syncDataField = healthStat
                        .GetType()
                        .GetField(
                            "SyncData",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        );
                    if (syncDataField != null)
                    {
                        var syncData = syncDataField.GetValue(healthStat);
                        if (syncData != null)
                        {
                            var syncDataType = syncData.GetType();
                            foreach (
                                var field in syncDataType.GetFields(
                                    BindingFlags.Instance
                                        | BindingFlags.Public
                                        | BindingFlags.NonPublic
                                )
                            )
                            {
                                var value = field.GetValue(syncData);
                                LoggerUtil.LogDebug(
                                    $"[KILLER_STAT] SyncData.{field.Name} = {value}"
                                );

                                if (
                                    field.Name.Contains("Attacker")
                                    || field.Name.Contains("Source")
                                    || field.Name.Contains("Dealer")
                                )
                                {
                                    if (value is long && (long)value != 0)
                                    {
                                        MyEntity attacker;
                                        if (MyEntities.TryGetEntityById((long)value, out attacker))
                                        {
                                            LoggerUtil.LogSuccess(
                                                $"[KILLER_STAT] Found attacker via SyncData: {attacker.DisplayName}"
                                            );
                                            AnalyzeDamageDealer(attacker, info);
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_STAT] Error: {ex.Message}");
                return false;
            }
        }

        private void AnalyzeDamageDealer(MyEntity damageDealer, KillerInfo info)
        {
            try
            {
                LoggerUtil.LogDebug($"[KILLER_ANALYZE] Entity type: {damageDealer.GetType().Name}");
                LoggerUtil.LogDebug($"[KILLER_ANALYZE] Display name: {damageDealer.DisplayName}");

                var attackerChar = damageDealer as IMyCharacter;
                if (attackerChar != null)
                {
                    info.IsPlayerKill = true;
                    info.KillerName = attackerChar.DisplayName;
                    info.Cause = DeathCause.Player;
                    info.WeaponName = "Weapon";
                    LoggerUtil.LogSuccess($"[KILLER_ANALYZE] Player kill: {info.KillerName}");
                    return;
                }

                var turret = damageDealer as IMyLargeTurretBase;
                if (turret != null)
                {
                    info.IsTurretKill = true;
                    info.Cause = DeathCause.Turret;
                    info.WeaponName = GetTurretTypeName(turret);
                    DetectTurretOwner(turret, info);
                    LoggerUtil.LogSuccess(
                        $"[KILLER_ANALYZE] Turret kill: {info.KillerName} with {info.WeaponName}"
                    );
                    return;
                }

                var grid = damageDealer as MyCubeGrid;
                if (grid != null)
                {
                    info.Cause = DeathCause.Collision;
                    info.KillerName = grid.DisplayName ?? "a ship";
                    info.WeaponName = "Collision";
                    LoggerUtil.LogSuccess($"[KILLER_ANALYZE] Grid collision: {info.KillerName}");
                    return;
                }

                LoggerUtil.LogWarning(
                    $"[KILLER_ANALYZE] Unknown damage dealer type: {damageDealer.GetType().Name}"
                );
                info.KillerName = damageDealer.DisplayName ?? "Unknown";
                info.WeaponName = "Unknown";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_ANALYZE] Error: {ex.Message}");
            }
        }

        private string GetTurretTypeName(IMyLargeTurretBase turret)
        {
            try
            {
                string subtypeName = turret.BlockDefinition.SubtypeName;
                if (subtypeName.Contains("Gatling"))
                    return "Gatling Turret";
                if (subtypeName.Contains("Missile"))
                    return "Missile Turret";
                if (subtypeName.Contains("Interior"))
                    return "Interior Turret";
                if (subtypeName.Contains("Rocket"))
                    return "Rocket Turret";
                if (subtypeName.Contains("Autocannon"))
                    return "Autocannon Turret";
                if (subtypeName.Contains("Artillery"))
                    return "Artillery Turret";
                return turret.CustomName ?? turret.DisplayName ?? "Turret";
            }
            catch
            {
                return "Turret";
            }
        }

        private void DetectTurretOwner(IMyLargeTurretBase turret, KillerInfo info)
        {
            try
            {
                var grid = turret.CubeGrid;
                if (grid == null)
                {
                    LoggerUtil.LogWarning("[KILLER_OWNER] Turret has no parent grid");
                    return;
                }

                var turretBlock = turret as MyCubeBlock;
                if (turretBlock == null || turretBlock.OwnerId == 0)
                {
                    LoggerUtil.LogWarning("[KILLER_OWNER] Turret has no owner");
                    return;
                }

                long ownerId = turretBlock.OwnerId;
                LoggerUtil.LogDebug($"[KILLER_OWNER] Owner ID: {ownerId}");

                var playerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);
                if (playerIdentity != null)
                {
                    info.TurretOwnerName = playerIdentity.DisplayName;
                    info.KillerName = playerIdentity.DisplayName;
                    info.IsPlayerKill = true;
                    LoggerUtil.LogSuccess(
                        $"[KILLER_OWNER] Player: {info.TurretOwnerName} (works for offline!)"
                    );
                    return;
                }

                var faction = MySession.Static.Factions.TryGetPlayerFaction(ownerId);
                if (faction != null)
                {
                    info.IsNpcFaction = !faction.AcceptHumans;
                    if (info.IsNpcFaction)
                    {
                        info.NpcFactionTag = faction.Tag;
                        info.KillerName = faction.Tag;
                        info.Cause = DeathCause.NpcFaction;
                        LoggerUtil.LogSuccess($"[KILLER_OWNER] NPC Faction: {info.NpcFactionTag}");
                    }
                    else
                    {
                        info.TurretOwnerName = faction.Name;
                        info.KillerName = faction.Name;
                        LoggerUtil.LogDebug(
                            $"[KILLER_OWNER] Player faction: {info.TurretOwnerName}"
                        );
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_OWNER] Error: {ex.Message}");
            }
        }

        private bool CheckOxygenDeath(MyCharacter character, KillerInfo info)
        {
            try
            {
                if (character.StatComp == null)
                    return false;

                // FIX: character.StatComp.Stats doesn't have GetStat().
                // We use TryGetStat() on the StatComp directly.
                MyEntityStat oxygenStat;
                if (
                    character.StatComp.TryGetStat(
                        MyStringHash.GetOrCompute("oxygen"),
                        out oxygenStat
                    )
                )
                {
                    if (oxygenStat != null && oxygenStat.Value <= 0.1f)
                    {
                        info.Cause = DeathCause.Oxygen;
                        info.KillerName = "Oxygen Depletion";
                        info.WeaponName = "Asphyxiation";
                        LoggerUtil.LogDebug($"[KILLER_OXYGEN] Oxygen: {oxygenStat.Value}");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_OXYGEN] Error: {ex.Message}");
                return false;
            }
        }

        private void DetectEnvironmentalDeath(MyCharacter character, KillerInfo info)
        {
            try
            {
                if (info.Cause == DeathCause.Unknown)
                {
                    info.Cause = DeathCause.Environment;
                    info.KillerName = "Environment";
                    info.WeaponName = "Accident";
                    LoggerUtil.LogDebug("[KILLER_ENV] Environmental death (fallback)");
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_ENV] Error: {ex.Message}");
            }
        }
    }
}
