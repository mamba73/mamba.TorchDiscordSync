// Services/DeathLocationService.cs
using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Planet;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace mamba.TorchDiscordSync.Services
{
    /// <summary>
    /// NEW: Service responsible for death location detection and zone-based messaging.
    /// Detects which zone a player died in (planet, inner system, outer space, deep space).
    /// Generates location-based descriptions and integrates grid/ship detection.
    /// </summary>
    public class DeathLocationService
    {
        // Configuration constants (in kilometers)
        private const double INNER_SYSTEM_MAX_KM = 5000.0;
        private const double OUTER_SPACE_MAX_KM = 10000.0;
        private const double PLANET_PROXIMITY_MULTIPLIER = 3.0;

        // Origin point for distance calculations (0,0,0)
        private static readonly Vector3D ORIGIN = Vector3D.Zero;

        /// <summary>
        /// Detects the death location zone and returns detailed location information.
        /// Called from PlayerTrackingService.OnCharacterDied() to determine location context.
        /// </summary>
        public LocationZoneResult DetectDeathZone(IMyCharacter character)
        {
            LoggerUtil.LogDebug("[LOCATION] Detecting death zone...");
            var result = new LocationZoneResult();

            try
            {
                if (character == null)
                {
                    LoggerUtil.LogWarning(
                        "DeathLocationService.DetectDeathZone: character is null"
                    );
                    result.Zone = LocationZoneEnum.UNKNOWN;
                    return result;
                }
                LoggerUtil.LogDebug(
                    $"[LOCATION] character {character.DisplayName} detected for location analysis"
                    );

                // Get character position from world matrix
                Vector3D deathPosition = character.GetPosition();
                result.DistanceFromOrigin = Vector3D.Distance(deathPosition, ORIGIN);

                // STEP 1: Check if death is near a planet (priority first)
                var nearestPlanet = FindNearestPlanet(deathPosition);
                if (nearestPlanet != null)
                {
                    double distanceToPlanet = Vector3D.Distance(
                        deathPosition,
                        nearestPlanet.PositionComp.GetPosition()
                    );
                    double planetProximityRadius =
                        nearestPlanet.AverageRadius * PLANET_PROXIMITY_MULTIPLIER;
                        
                    LoggerUtil.LogDebug(
                        $"[LOCATION] planetProximityRadius= {planetProximityRadius:F2} km for {character.DisplayName}"
                    );
                    LoggerUtil.LogDebug(
                        $"[LOCATION] distanceToPlanet= {distanceToPlanet:F2}"
                    );
                    LoggerUtil.LogDebug(
                        $"[LOCATION] nearestPlanet.AverageRadius= {nearestPlanet.AverageRadius:F2} km"
                    );
                    LoggerUtil.LogDebug(
                        $"[LOCATION] nearestPlanet.DisplayNameText= {nearestPlanet.DisplayNameText}"
                    );
                    LoggerUtil.LogDebug(
                        $"[LOCATION] PLANET_PROXIMITY_MULTIPLIER= {PLANET_PROXIMITY_MULTIPLIER:F2}"
                    );

                    if (distanceToPlanet < planetProximityRadius)
                    {
                        result.Zone = LocationZoneEnum.AROUND_PLANET;
                        result.PlanetName = nearestPlanet.DisplayNameText ?? "Unknown Planet";
                        result.DistanceToPlanet = distanceToPlanet;
                        LoggerUtil.LogDebug(
                            $"[LOCATION] Death near planet: {result.PlanetName} (distance: {distanceToPlanet:F2} km)"
                        );
                        return result;
                    }
                }

                // STEP 2: Classify by distance from origin
                if (result.DistanceFromOrigin < INNER_SYSTEM_MAX_KM)
                {
                    result.Zone = LocationZoneEnum.INNER_SYSTEM;
                    LoggerUtil.LogDebug(
                        $"[LOCATION] Death in inner system (distance: {result.DistanceFromOrigin:F2} km)"
                    );
                }
                else if (result.DistanceFromOrigin < OUTER_SPACE_MAX_KM)
                {
                    result.Zone = LocationZoneEnum.OUTER_SPACE;
                    LoggerUtil.LogDebug(
                        $"[LOCATION] Death in outer space (distance: {result.DistanceFromOrigin:F2} km)"
                    );
                }
                else
                {
                    result.Zone = LocationZoneEnum.DEEP_SPACE;
                    LoggerUtil.LogDebug(
                        $"[LOCATION] Death in deep space (distance: {result.DistanceFromOrigin:F2} km)"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in DetectDeathZone: {ex.Message}");
                result.Zone = LocationZoneEnum.UNKNOWN;
                return result;
            }
        }

        /// <summary>
        /// NEW: Detects if character is inside a grid/ship and returns grid information.
        /// Grid detection is called separately to allow for optional inclusion in messages.
        /// </summary>
        public void DetectGridContext(IMyCharacter character, LocationZoneResult result)
        {
            try
            {
                if (character == null || result == null)
                    return;

                // Try to find parent grid if character is inside one
                var myCharacter = character as MyCharacter;
                if (myCharacter == null)
                    return;

                // Get parent entity (could be a grid)
                var parent = myCharacter.Parent;
                if (parent == null)
                    return;

                var grid = parent as MyCubeGrid;
                if (grid == null)
                    return;

                // Character is inside a grid
                result.IsInsideGrid = true;
                result.GridName = grid.DisplayName ?? grid.Name ?? "Unknown Ship";
                LoggerUtil.LogDebug($"[LOCATION] Character inside grid: {result.GridName}");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in DetectGridContext: {ex.Message}");
            }
        }

        /// <summary>
        /// NEW: Generates location-based text for death messages.
        /// Used by DeathLogService.GenerateDeathMessage() to create zone-specific descriptions.
        /// </summary>
        public string GenerateLocationText(LocationZoneResult zoneResult, bool includeGridName)
        {
            if (zoneResult == null)
                return "at unknown location";

            try
            {
                string zoneText = GetZoneDescription(zoneResult);

                // If grid detection is enabled and character was inside a grid
                if (
                    includeGridName
                    && zoneResult.IsInsideGrid
                    && !string.IsNullOrEmpty(zoneResult.GridName)
                )
                {
                    return $"inside {zoneResult.GridName} {zoneText}";
                }
                else if (includeGridName && zoneResult.IsInsideGrid)
                {
                    return $"inside a ship {zoneText}";
                }

                return zoneText;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in GenerateLocationText: {ex.Message}");
                return "at unknown location";
            }
        }

        /// <summary>
        /// NEW: Gets a zone-specific description for the death location.
        /// Returns randomized descriptions based on zone classification.
        /// </summary>
        private string GetZoneDescription(LocationZoneResult result)
        {
            try
            {
                switch (result.Zone)
                {
                    case LocationZoneEnum.AROUND_PLANET:
                        return GetRandomPlanetMessage(result.PlanetName);

                    case LocationZoneEnum.INNER_SYSTEM:
                        return GetRandomInnerSystemMessage();

                    case LocationZoneEnum.OUTER_SPACE:
                        return GetRandomOuterSpaceMessage();

                    case LocationZoneEnum.DEEP_SPACE:
                        return GetRandomDeepSpaceMessage();

                    case LocationZoneEnum.UNKNOWN:
                    default:
                        return "at unknown location";
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in GetZoneDescription: {ex.Message}");
                return "at unknown location";
            }
        }

        /// <summary>
        /// NEW: Gets random planet-based message from hardcoded variations.
        /// </summary>
        private string GetRandomPlanetMessage(string planetName)
        {
            var messages = new List<string>
            {
                $"near {planetName}",
                $"above {planetName}",
                $"outside {planetName}'s atmosphere",
                $"orbiting {planetName}",
            };

            return messages[new Random().Next(messages.Count)];
        }

        /// <summary>
        /// NEW: Gets random inner system message from hardcoded variations.
        /// </summary>
        private string GetRandomInnerSystemMessage()
        {
            var messages = new List<string>
            {
                "in the inner system",
                "between planets",
                "near the star",
                "in the system center",
            };

            return messages[new Random().Next(messages.Count)];
        }

        /// <summary>
        /// NEW: Gets random outer space message from hardcoded variations.
        /// </summary>
        private string GetRandomOuterSpaceMessage()
        {
            var messages = new List<string>
            {
                "in outer space",
                "on the edge of the system",
                "in the void",
                "far from everything",
            };

            return messages[new Random().Next(messages.Count)];
        }

        /// <summary>
        /// NEW: Gets random deep space message from hardcoded variations.
        /// </summary>
        private string GetRandomDeepSpaceMessage()
        {
            var messages = new List<string>
            {
                "in deep space",
                "lost in the void",
                "at the edge of the universe",
                "nowhere to be found",
            };

            return messages[new Random().Next(messages.Count)];
        }

        /// <summary>
        /// NEW: Finds the nearest planet to the given position.
        /// Returns null if no planets exist in the session.
        /// </summary>
        private MyPlanet FindNearestPlanet(Vector3D position)
        {
            try
            {
                if (MyPlanets.Static == null)
                    return null;

                var planets = MyPlanets.GetPlanets();
                if (planets == null || planets.Count == 0)
                    return null;

                MyPlanet nearest = null;
                double nearestDistance = double.MaxValue;

                foreach (var planet in planets)
                {
                    if (planet == null)
                        continue;

                    double distance = Vector3D.Distance(
                        position,
                        planet.PositionComp.GetPosition()
                    );
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = planet;
                    }
                }

                return nearest;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in FindNearestPlanet: {ex.Message}");
                return null;
            }
        }
    }
}
