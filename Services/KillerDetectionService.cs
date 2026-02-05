// Services/KillerDetectionService.cs
using System;
using System.Linq;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Utils;
using Sandbox.Game.Entities.Character.Components;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// Service for detecting who/what killed a player
    /// Handles: Players, Turrets (with owner detection), NPC factions, Environmental deaths
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
            Environment
        }

        /// <summary>
        /// Detects who/what killed the character by examining last damage source
        /// This is called DURING the CharacterDied event, before the character is fully disposed
        /// </summary>
        public KillerInfo DetectKiller(IMyCharacter victim)
        {
            var info = new KillerInfo();

            try
            {
                if (victim == null)
                {
                    LoggerUtil.LogDebug("[KILLER_DETECT] Victim is null");
                    return info;
                }

                LoggerUtil.LogDebug($"[KILLER_DETECT] ═══ Starting killer detection for {victim.DisplayName} ═══");

                // Cast to internal character for access to damage history
                var myCharacter = victim as Sandbox.Game.Entities.Character.MyCharacter;
                if (myCharacter == null)
                {
                    LoggerUtil.LogDebug("[KILLER_DETECT] Cannot cast to MyCharacter");
                    return info;
                }

                // Try to get last damage info
                var lastDamageInfo = GetLastDamageInfo(myCharacter);

                if (lastDamageInfo.HasValue)
                {
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Damage Type: {lastDamageInfo.Value.Type}");
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Damage Amount: {lastDamageInfo.Value.Amount}");

                    // Check attacker entity
                    if (lastDamageInfo.Value.AttackerId != 0)
                    {
                        MyEntity attackerEntity;
                        if (MyEntities.TryGetEntityById(lastDamageInfo.Value.AttackerId, out attackerEntity))
                        {
                            LoggerUtil.LogDebug($"[KILLER_DETECT] Attacker Entity: {attackerEntity?.GetType().Name} - {attackerEntity?.DisplayName}");
                            AnalyzeAttacker(attackerEntity, info);
                        }
                        else
                        {
                            LoggerUtil.LogDebug($"[KILLER_DETECT] Attacker entity ID {lastDamageInfo.Value.AttackerId} not found");
                        }
                    }

                    // Analyze damage type
                    AnalyzeDamageType(lastDamageInfo.Value, info);
                }
                else
                {
                    LoggerUtil.LogDebug("[KILLER_DETECT] No damage info available");
                }

                // Fallback: Check environment/health
                if (info.Cause == DeathCause.Unknown)
                {
                    DetectEnvironmentalDeath(myCharacter, info);
                }

                LoggerUtil.LogDebug($"[KILLER_DETECT] Final Result: Cause={info.Cause}, Killer={info.KillerName}, Weapon={info.WeaponName}");
                LoggerUtil.LogDebug($"[KILLER_DETECT] ═══ Detection complete ═══");

                return info;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] Error: {ex.Message}\n{ex.StackTrace}");
                return info;
            }
        }

        /// <summary>
        /// Gets last damage information from character
        /// </summary>
        private MyDamageInformation? GetLastDamageInfo(Sandbox.Game.Entities.Character.MyCharacter character)
        {
            try
            {
                // Note: In actual Torch environment, you'd likely use Reflection here to get m_lastDamage
                if (character.StatComp != null)
                {
                    LoggerUtil.LogDebug("[KILLER_DETECT] StatComp available but no direct damage history access");
                }

                return null; 
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] GetLastDamageInfo error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes attacker entity to determine killer type
        /// </summary>
        private void AnalyzeAttacker(MyEntity attacker, KillerInfo info)
        {
            try
            {
                // Check if attacker is another player's character
                var attackerChar = attacker as IMyCharacter;
                if (attackerChar != null)
                {
                    info.IsPlayerKill = true;
                    info.KillerName = attackerChar.DisplayName;
                    info.Cause = DeathCause.Player;
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Player kill detected: {info.KillerName}");
                    return;
                }

                // Check if attacker is a weapon/turret
                var turret = attacker as IMyLargeTurretBase;
                if (turret != null)
                {
                    info.IsTurretKill = true;
                    info.WeaponName = turret.DisplayName ?? turret.DefinitionDisplayNameText ?? "Turret";
                    info.Cause = DeathCause.Turret;

                    // Get turret owner
                    DetectTurretOwner(turret, info);

                    LoggerUtil.LogDebug($"[KILLER_DETECT] Turret kill: {info.WeaponName}, Owner: {info.TurretOwnerName ?? info.NpcFactionTag ?? "Unknown"}");
                    return;
                }

                // Check if attacker is a gun/handheld weapon
                var gun = attacker as IMyGunBaseUser;
                if (gun != null)
                {
                    info.WeaponName = gun.ToString();
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Gun detected: {info.WeaponName}");

                    // Try to find owner of the gun
                    var gunOwner = FindGunOwner(gun);
                    if (gunOwner != null)
                    {
                        info.IsPlayerKill = true;
                        info.KillerName = gunOwner.DisplayName;
                        info.Cause = DeathCause.Player;
                    }
                    return;
                }

                // Check if it's a grid collision
                var grid = attacker as MyCubeGrid;
                if (grid != null)
                {
                    info.Cause = DeathCause.Collision;
                    info.KillerName = grid.DisplayName ?? "a ship";
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Grid collision: {info.KillerName}");
                    return;
                }

                LoggerUtil.LogDebug($"[KILLER_DETECT] Unknown attacker type: {attacker.GetType().Name}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] AnalyzeAttacker error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects turret owner - either player or NPC faction
        /// CRITICAL: This handles offline players and NPC factions!
        /// </summary>
        private void DetectTurretOwner(IMyLargeTurretBase turret, KillerInfo info)
        {
            try
            {
                var grid = turret.CubeGrid;
                if (grid == null)
                {
                    LoggerUtil.LogDebug("[KILLER_DETECT] Turret has no parent grid");
                    return;
                }

                // Get block owner
                var turretBlock = turret as MyCubeBlock;
                if (turretBlock != null && turretBlock.OwnerId != 0)
                {
                    long ownerId = turretBlock.OwnerId;
                    LoggerUtil.LogDebug($"[KILLER_DETECT] Turret owned by: {ownerId}");

                    // Try to find owner as player (online or offline)
                    var playerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);
                    if (playerIdentity != null)
                    {
                        info.TurretOwnerName = playerIdentity.DisplayName;
                        info.KillerName = playerIdentity.DisplayName;
                        info.IsPlayerKill = true; 
                        LoggerUtil.LogDebug($"[KILLER_DETECT] Turret owner (player): {info.TurretOwnerName}");
                        return;
                    }

                    // Check if owner is an NPC faction
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(ownerId);
                    if (faction != null)
                    {
                        info.IsNpcFaction = !faction.AcceptHumans; 
                        if (info.IsNpcFaction)
                        {
                            info.NpcFactionTag = faction.Tag;
                            info.KillerName = faction.Tag;
                            info.Cause = DeathCause.NpcFaction;
                            LoggerUtil.LogDebug($"[KILLER_DETECT] NPC Faction turret: {info.NpcFactionTag}");
                            return;
                        }
                        else
                        {
                            info.TurretOwnerName = faction.Name;
                            info.KillerName = faction.Name;
                            LoggerUtil.LogDebug($"[KILLER_DETECT] Player faction turret: {info.TurretOwnerName}");
                            return;
                        }
                    }
                }

                LoggerUtil.LogDebug("[KILLER_DETECT] Could not determine turret owner");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] DetectTurretOwner error: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to find the player holding a gun
        /// </summary>
        private IMyCharacter FindGunOwner(IMyGunBaseUser gun)
        {
            try
            {
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);

                foreach (var player in players)
                {
                    if (player?.Character == null)
                        continue;

                    var character = player.Character as Sandbox.Game.Entities.Character.MyCharacter;
                    if (character != null && character.CurrentWeapon != null)
                    {
                        if (character.CurrentWeapon.Equals(gun))
                        {
                            return player.Character;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] FindGunOwner error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes damage type to determine cause of death
        /// </summary>
        private void AnalyzeDamageType(MyDamageInformation damageInfo, KillerInfo info)
        {
            try
            {
                string damageType = damageInfo.Type.ToString();
                LoggerUtil.LogDebug($"[KILLER_DETECT] Analyzing damage type: {damageType}");

                if (damageType.Contains("Bullet") || damageType.Contains("Rocket") || damageType.Contains("Missile"))
                {
                    if (info.Cause == DeathCause.Unknown)
                        info.Cause = DeathCause.Player;
                }
                else if (damageType.Contains("Fall"))
                {
                    info.Cause = DeathCause.Fall;
                }
                else if (damageType.Contains("Environment") || damageType.Contains("Asphyxia"))
                {
                    info.Cause = DeathCause.Environment;
                }
                else if (damageType.Contains("Suicide"))
                {
                    info.Cause = DeathCause.Suicide;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] AnalyzeDamageType error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects environmental death causes (oxygen, pressure, etc.)
        /// </summary>
        private void DetectEnvironmentalDeath(Sandbox.Game.Entities.Character.MyCharacter character, KillerInfo info)
        {
            try
            {
                if (character.StatComp == null)
                    return;

                // Check oxygen
                MyEntityStat oxygenStat;
                if (character.StatComp.TryGetStat(MyStringHash.GetOrCompute("oxygen"), out oxygenStat))
                {
                    if (oxygenStat != null && oxygenStat.Value <= 0)
                    {
                        info.Cause = DeathCause.Oxygen;
                        info.KillerName = "Asphyxiation";
                        LoggerUtil.LogDebug("[KILLER_DETECT] Death by oxygen");
                        return;
                    }
                }

                // Check health
                MyEntityStat healthStat;
                if (character.StatComp.TryGetStat(MyStringHash.GetOrCompute("health"), out healthStat))
                {
                    if (healthStat != null && healthStat.Value <= 0)
                    {
                        info.Cause = DeathCause.Environment;
                        LoggerUtil.LogDebug("[KILLER_DETECT] Environmental death");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] DetectEnvironmentalDeath error: {ex.Message}");
            }
        }
    }
}
