// Services/KillerDetectionService.cs
using System;
using System.Linq;
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
using VRage.Utils; // Potrebno za MyStringHash
using Sandbox.Game.Entities.Character.Components; // Potrebno za IMyEntityStat

namespace mamba.TorchDiscordSync.Services
{
    public class KillerDetectionService
    {
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

        public KillerInfo DetectKiller(IMyCharacter victim)
        {
            var info = new KillerInfo();
            try
            {
                if (victim == null) return info;

                var myCharacter = victim as Sandbox.Game.Entities.Character.MyCharacter;
                if (myCharacter == null) return info;

                // Ovdje bi išla logika za damage info ako je implementiraš
                if (info.Cause == DeathCause.Unknown)
                {
                    DetectEnvironmentalDeath(myCharacter, info);
                }

                return info;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] Error: {ex.Message}");
                return info;
            }
        }

        private void DetectTurretOwner(IMyLargeTurretBase turret, KillerInfo info)
        {
            try
            {
                var turretBlock = turret as MyCubeBlock;
                if (turretBlock != null && turretBlock.OwnerId != 0)
                {
                    // MySession.Static sada radi jer imamo Sandbox.Game.World
                    var playerIdentity = MySession.Static.Players.TryGetIdentity(turretBlock.OwnerId);
                    if (playerIdentity != null)
                    {
                        info.KillerName = playerIdentity.DisplayName;
                        info.IsPlayerKill = true;
                        return;
                    }

                    var faction = MySession.Static.Factions.TryGetPlayerFaction(turretBlock.OwnerId);
                    if (faction != null)
                    {
                        info.IsNpcFaction = !faction.AcceptHumans;
                        info.NpcFactionTag = faction.Tag;
                        info.KillerName = faction.Tag;
                        info.Cause = DeathCause.NpcFaction;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[KILLER_DETECT] DetectTurretOwner error: {ex.Message}");
            }
        }

        private void DetectEnvironmentalDeath(Sandbox.Game.Entities.Character.MyCharacter character, KillerInfo info)
        {
            try
            {
                // StatComp je MyEntityStatComponent. 
                // TryGetStat se poziva direktno na StatComp, a ne na StatComp.Stats!
                if (character.StatComp == null)
                    return;

                // Definiramo hash-eve unaprijed
                var oxygenHash = MyStringHash.GetOrCompute("oxygen");
                var healthHash = MyStringHash.GetOrCompute("health");

                // Popravak za Line 126 i 137: Pozivamo TryGetStat na StatComp
                MyEntityStat oxygenStat;
                if (character.StatComp.TryGetStat(oxygenHash, out oxygenStat))
                {
                    if (oxygenStat != null && oxygenStat.Value <= 0)
                    {
                        info.Cause = DeathCause.Oxygen;
                        info.KillerName = "Asphyxiation";
                        LoggerUtil.LogDebug("[KILLER_DETECT] Death by oxygen (Stat check)");
                        return;
                    }
                }

                MyEntityStat healthStat;
                if (character.StatComp.TryGetStat(healthHash, out healthStat))
                {
                    // Ako je health 0, a nismo našli drugog napadača
                    if (healthStat != null && healthStat.Value <= 0 && info.Cause == DeathCause.Unknown)
                    {
                        info.Cause = DeathCause.Environment;
                        LoggerUtil.LogDebug("[KILLER_DETECT] Environmental death (Stat check)");
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