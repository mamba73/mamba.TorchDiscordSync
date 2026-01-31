// Models/DeathTypeEnum.cs
using System;

namespace mamba.TorchDiscordSync.Models
{
    /// <summary>
    /// Enumeration of different death types for categorizing player deaths.
    /// Used by DeathLogService to select appropriate death messages.
    /// </summary>
    [Serializable]
    public enum DeathTypeEnum
    {
        /// <summary>
        /// Unknown or unclassified death type
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Player died to their own actions or weapons
        /// </summary>
        Suicide = 1,

        /// <summary>
        /// First kill by this player against the victim
        /// </summary>
        FirstKill = 2,

        /// <summary>
        /// Retaliation kill (victim killed the killer recently)
        /// </summary>
        Retaliation = 3,

        /// <summary>
        /// Old retaliation kill (victim killed the killer, but not recently)
        /// </summary>
        RetaliationOld = 4,

        /// <summary>
        /// Accidental death (non-PvP, non-environmental)
        /// </summary>
        Accident = 5,

        /// <summary>
        /// Death caused by lack of oxygen
        /// </summary>
        Environment_Oxygen = 6,

        /// <summary>
        /// Death caused by pressure damage (depressurization)
        /// </summary>
        Environment_Pressure = 7,

        /// <summary>
        /// Death caused by collision or fall damage
        /// </summary>
        Environment_Collision = 8,

        /// <summary>
        /// Death caused by a grid (ship/station crushing)
        /// </summary>
        Grid = 9,

        /// <summary>
        /// Player vs Player combat death
        /// </summary>
        PvP = 10
    }
}