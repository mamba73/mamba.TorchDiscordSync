// Models/DeathTypeEnum.cs
using System;
using System.Xml.Serialization;

namespace mamba.TorchDiscordSync.Models
{
    /// <summary>
    /// Enumeration of death types for categorization and message templates
    /// Used by DeathLogService to determine which message template to use
    /// </summary>
    [Serializable]
    [XmlType("DeathType")]
    public enum DeathTypeEnum
    {
        /// <summary>
        /// Player killed themselves (no attacker or attacker is victim)
        /// Template: "{0} forgot they needed oxygen."
        /// </summary>
        [XmlEnum("Suicide")]
        Suicide = 0,

        /// <summary>
        /// Player's first kill (victim never killed killer before)
        /// Template: "{0} obliterated {1} with {2}."
        /// </summary>
        [XmlEnum("FirstKill")]
        FirstKill = 1,

        /// <summary>
        /// Recent retaliation (victim killed killer within 1 hour)
        /// Template: "{0} got their revenge on {1}."
        /// </summary>
        [XmlEnum("Retaliation")]
        Retaliation = 2,

        /// <summary>
        /// Old retaliation (victim killed killer between 1-24 hours ago)
        /// Template: "{0} finally caught up with {1}."
        /// </summary>
        [XmlEnum("RetaliationOld")]
        RetaliationOld = 3,

        /// <summary>
        /// Accident or environmental death (no player involved)
        /// Template: "{0} didn't expect gravity to be that strong."
        /// </summary>
        [XmlEnum("Accident")]
        Accident = 4,

        /// <summary>
        /// Unknown death type (fallback)
        /// </summary>
        [XmlEnum("Unknown")]
        Unknown = 5
    }
}