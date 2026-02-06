// Services/DeathLocationService.cs
using System;
using System.Collections.Generic;
using mamba.TorchDiscordSync.Config;
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
    /// Service responsible for death location detection and zone-based messaging.
    /// Detects which zone a player died in (planet, inner system, outer space, deep space).
    /// Generates location-based descriptions and integrates grid/ship detection.
    /// </summary>
    public class DeathLocationService
    {
        private readonly MainConfig _config;

        // Default constants (fallback values if config not available)
        private const double DEFAULT_INNER_SYSTEM_MAX_KM = 5000.0;
        private const double DEFAULT_OUTER_SPACE_MAX_KM = 10000.0;
        private const double DEFAULT_PLANET_PROXIMITY_MULTIPLIER = 3.0;

        // Origin point for distance calculations (0,0,0)
        private static readonly Vector3D ORIGIN = Vector3D.Zero;

        // Properties that read from config with fallback to defaults
        private double InnerSystemMaxKm => _config?.Death?.InnerSystemMaxKm ?? DEFAULT_INNER_SYSTEM_MAX_KM;
        private double OuterSpaceMaxKm => _config?.Death?.OuterSpaceMaxKm ?? DEFAULT_OUTER_SPACE_MAX_KM;
        private double PlanetProximityMultiplier => _config?.Death?.PlanetProximityMultiplier ?? DEFAULT_PLANET_PROXIMITY_MULTIPLIER;

        public DeathLocationService(MainConfig config = null)
        {
            _config = config;
            LoggerUtil.LogDebug("[LOCATION] DeathLocationService initialized with config");
        }

        /// <summary>
        /// Detects the death location zone and returns detailed location information.
        /// Called from PlayerTrackingService.OnCharacterDied() to determine location context.
        /// </summary>
        public LocationZoneResult DetectDeathZone(IMyCharacter character)
        {
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
                        nearestPlanet.AverageRadius * PlanetProximityMultiplier;

                    // DEBUG: Print all planet properties
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] ===== PLANET DEBUG INFO =====");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] DisplayNameText: '{nearestPlanet.DisplayNameText}'");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] StorageName: '{nearestPlanet.StorageName}'");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Name: '{nearestPlanet.Name}'");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] DisplayName: '{nearestPlanet.DisplayName}'");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] AverageRadius: {nearestPlanet.AverageRadius} km");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Distance to planet: {distanceToPlanet:F2} km");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Proximity radius: {planetProximityRadius:F2} km");
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] ===== END DEBUG INFO =====");

                    if (distanceToPlanet < planetProximityRadius)
                    {
                        result.Zone = LocationZoneEnum.AROUND_PLANET;

                        // Use enhanced planet name detection
                        string planetName = GetPlanetName(nearestPlanet);
                        result.PlanetName = planetName;
                        result.DistanceToPlanet = distanceToPlanet;

                        LoggerUtil.LogDebug(
                            $"[LOCATION] Death near planet: {result.PlanetName} (distance: {distanceToPlanet:F2} km)"
                        );
                        return result;
                    }
                }

                // STEP 2: Classify by distance from origin
                if (result.DistanceFromOrigin < InnerSystemMaxKm)
                {
                    result.Zone = LocationZoneEnum.INNER_SYSTEM;
                    LoggerUtil.LogDebug(
                        $"[LOCATION] Death in inner system (distance: {result.DistanceFromOrigin:F2} km)"
                    );
                }
                else if (result.DistanceFromOrigin < OuterSpaceMaxKm)
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

                // STEP 3: Detect if inside a grid
                DetectGridContext(character, result);

                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in DetectDeathZone: {ex.Message}\n{ex.StackTrace}");
                result.Zone = LocationZoneEnum.UNKNOWN;
                return result;
            }
        }

        /// <summary>
        /// Enhanced method for finding planet/moon name.
        /// Tries multiple properties without using non-existent ChildList.
        /// </summary>
        private string GetPlanetName(MyPlanet planet)
        {
            try
            {
                // Try each property in order - NO CHILDLIST!
                if (!string.IsNullOrEmpty(planet.DisplayNameText))
                {
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Selected name from DisplayNameText: {planet.DisplayNameText}");
                    // DisplayNameText: ''
                    return planet.DisplayNameText;
                }

                if (!string.IsNullOrEmpty(planet.Name))
                {
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Selected name from Name: {planet.Name}");
                    // Name: 'Moon'
                    return planet.Name;
                }
                if (!string.IsNullOrEmpty(planet.StorageName))
                {
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Selected name from StorageName: {planet.StorageName}");
                    // StorageName: 'Moon-1353915701d19000'
                    return planet.StorageName;
                }

                if (!string.IsNullOrEmpty(planet.DisplayName))
                {
                    LoggerUtil.LogDebug($"[LOCATION_DEBUG] Selected name from DisplayName: {planet.DisplayName}");
                    // DisplayName: ''
                    return planet.DisplayName;
                }

                LoggerUtil.LogDebug($"[LOCATION_DEBUG] No valid planet name found, using Unknown Planet");
                return "Unknown Planet";
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"Error in GetPlanetName: {ex.Message}");
                return "Unknown Planet";
            }
        }

        /// <summary>
        /// Detects if character is inside a grid/ship and returns grid information.
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
        /// Generates location-based text for death messages.
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
        /// Gets a zone-specific description for the death location.
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
        /// Gets random planet-based message.
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
        /// Gets random inner system message.
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
        /// Gets random outer space message.
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
        /// Gets random deep space message.
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
        /// Finds the nearest planet to the given position.
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