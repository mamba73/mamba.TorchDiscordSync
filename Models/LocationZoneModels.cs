// Models/LocationZoneModels.cs
using System;

namespace mamba.TorchDiscordSync.Models
{
    /// <summary>
    /// NEW: Enumeration for death location zones based on distance from origin.
    /// Determines which message templates to use for death announcements.
    /// </summary>
    public enum LocationZoneEnum
    {
        /// <summary>Within 3x planet radius - death near a planet surface/atmosphere</summary>
        AROUND_PLANET,

        /// <summary>0-5000 km from origin - inner system with planets and stations</summary>
        INNER_SYSTEM,

        /// <summary>5000-10000 km from origin - outer system area</summary>
        OUTER_SPACE,

        /// <summary>Beyond 10000 km - deep space/wilderness</summary>
        DEEP_SPACE,

        /// <summary>Unknown or unable to determine</summary>
        UNKNOWN,
    }

    /// <summary>
    /// NEW: Container for location detection results.
    /// Used internally by DeathLocationService to communicate zone info.
    /// </summary>
    public class LocationZoneResult
    {
        /// <summary>Detected zone based on distance calculations</summary>
        public LocationZoneEnum Zone { get; set; }

        /// <summary>Planet name if AROUND_PLANET, otherwise empty string</summary>
        public string PlanetName { get; set; }

        /// <summary>Distance from origin in kilometers</summary>
        public double DistanceFromOrigin { get; set; }

        /// <summary>Distance to nearest planet in kilometers, if applicable</summary>
        public double DistanceToPlanet { get; set; }

        /// <summary>Grid/ship name if character is inside one, otherwise null</summary>
        public string GridName { get; set; }

        /// <summary>Whether the character is inside a grid</summary>
        public bool IsInsideGrid { get; set; }

        public LocationZoneResult()
        {
            Zone = LocationZoneEnum.UNKNOWN;
            PlanetName = string.Empty;
            DistanceFromOrigin = 0;
            DistanceToPlanet = 0;
            GridName = null;
            IsInsideGrid = false;
        }
    }
}
