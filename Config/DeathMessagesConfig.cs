// Config/DeathMessagesConfig.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using mamba.TorchDiscordSync.Models;
using mamba.TorchDiscordSync.Utils;

namespace mamba.TorchDiscordSync.Config
{
    /// <summary>
    /// Configuration for death message templates.
    /// Contains randomized messages for different death types.
    /// </summary>
    [XmlRoot("DeathMessages")]
    public class DeathMessagesConfig
    {
        private static Random _random = new Random();

        [XmlArray("Suicide")]
        [XmlArrayItem("Message")]
        public List<string> SuicideMessages { get; set; } = new List<string>
        {
            "{victim} committed suicide",
            "{victim} chose the easy way out",
            "{victim} decided to leave this world",
            "{victim} embraced the void",
            "{victim} pressed the big red button... on themselves",
            "{victim} became a permanent part of the scenery"
        };

        [XmlArray("PvP")]
        [XmlArrayItem("Message")]
        public List<string> PvPMessages { get; set; } = new List<string>
        {
            "{killer} killed {victim} with {weapon}",
            "{victim} was eliminated by {killer} using {weapon}",
            "{killer} blasted {victim} into space dust with {weapon}",
            "{killer} sent {victim} to the respawn screen with {weapon}",
            "{victim} tried to parry {killer}'s {weapon} with their face",
            "{killer} introduced {victim} to their little friend: {weapon}"
        };

        [XmlArray("Environment_Oxygen")]
        [XmlArrayItem("Message")]
        public List<string> EnvironmentOxygenMessages { get; set; } = new List<string>
        {
            "{victim} died from lack of oxygen",
            "{victim} held their breath for too long in the vacuum",
            "{victim} forgot to refill their oxygen tanks",
            "{victim} realized too late that space is empty",
            "{victim} tried to breathe the aesthetics, but needed oxygen",
            "{victim} is now a very cold, very breathless popsicle"
        };

        [XmlArray("Environment_Pressure")]
        [XmlArrayItem("Message")]
        public List<string> EnvironmentPressureMessages { get; set; } = new List<string>
        {
            "{victim} died from environmental pressure",
            "{victim} couldn't handle the pressure",
            "{victim} was victimized by sudden depressurization",
            "{victim} popped like a balloon",
            "{victim}'s suit had a 'minor' leak",
            "{victim} discovered that ears aren't supposed to do that"
        };

        [XmlArray("Environment_Collision")]
        [XmlArrayItem("Message")]
        public List<string> EnvironmentCollisionMessages { get; set; } = new List<string>
        {
            "{victim} hit something very fast",
            "{victim} fell from a great height",
            "{victim} died in a collision",
            "{victim} slammed into an asteroid",
            "{victim} forgot how brakes work",
            "{victim} successfully turned into a pancake"
        };

        [XmlArray("Grid")]
        [XmlArrayItem("Message")]
        public List<string> GridMessages { get; set; } = new List<string>
        {
            "{victim} was run over by a grid",
            "{victim} died in a collision with a ship",
            "{victim} was crushed between grids",
            "{victim} met the business end of a landing gear",
            "Lord Clang claimed {victim} as a sacrifice",
            "{victim} was personalized by a moving ship"
        };

        [XmlArray("FirstKill")]
        [XmlArrayItem("Message")]
        public List<string> FirstKillMessages { get; set; } = new List<string>
        {
            "🩸 FIRST BLOOD! {killer} took their first victim - {victim}",
            "⚔️ {killer} started a massacre with {victim}!",
            "📢 Attention! {killer} has opened the hunting season on {victim}!",
            "💀 The first sacrifice has been made: {victim} fell to {killer}!"
        };

        [XmlArray("Retaliation")]
        [XmlArrayItem("Message")]
        public List<string> RetaliationMessages { get; set; } = new List<string>
        {
            "💀 RETALIATION! {killer} got revenge on {victim}",
            "⚡ {killer} strikes back against {victim}!",
            "🔄 The tables have turned! {killer} just schooled {victim}",
            "⚖️ Karma is a ship: {killer} just rammed justice into {victim}"
        };

        [XmlArray("Accident")]
        [XmlArrayItem("Message")]
        public List<string> AccidentMessages { get; set; } = new List<string>
        {
            "{victim} died in an accident",
            "{victim} is no more",
            "{victim} met an unfortunate end",
            "{victim} experienced a rapid unplanned disassembly",
            "{victim} disconnected from life unexpectedly",
            "Error 404: {victim}'s pulse not found"
        };

        public string GetRandomMessage(DeathTypeEnum deathType)
        {
            List<string> messages = GetMessagesForType(deathType);

            if (messages == null || messages.Count == 0)
                return "{victim} died"; // Fallback

            int index = _random.Next(messages.Count);
            return messages[index];
        }

        private List<string> GetMessagesForType(DeathTypeEnum deathType)
        {
            switch (deathType)
            {
                case DeathTypeEnum.Suicide:
                    return SuicideMessages;
                case DeathTypeEnum.PvP:
                    return PvPMessages;
                case DeathTypeEnum.FirstKill:
                    return FirstKillMessages;
                case DeathTypeEnum.Retaliation:
                case DeathTypeEnum.RetaliationOld:
                    return RetaliationMessages;
                case DeathTypeEnum.Grid:
                    return GridMessages;
                case DeathTypeEnum.Environment_Oxygen:
                    return EnvironmentOxygenMessages;
                case DeathTypeEnum.Environment_Pressure:
                    return EnvironmentPressureMessages;
                case DeathTypeEnum.Environment_Collision:
                    return EnvironmentCollisionMessages;
                case DeathTypeEnum.Accident:
                case DeathTypeEnum.Unknown:
                default:
                    return AccidentMessages;
            }
        }

        public static DeathMessagesConfig Load()
        {
            string path = Path.Combine("Instance", "mambaTorchDiscordSync", "DeathMessages.xml");
            try
            {
                if (File.Exists(path))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(DeathMessagesConfig));
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        return (DeathMessagesConfig)serializer.Deserialize(fs);
                    }
                }
                else
                {
                    var config = new DeathMessagesConfig();
                    config.Save();
                    return config;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Failed to load DeathMessages.xml: " + ex.Message);
                return new DeathMessagesConfig();
            }
        }

        public void Save()
        {
            string path = Path.Combine("Instance", "mambaTorchDiscordSync", "DeathMessages.xml");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                XmlSerializer serializer = new XmlSerializer(typeof(DeathMessagesConfig));
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(fs, this);
                }
                LoggerUtil.LogInfo("DeathMessages.xml saved");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError("Failed to save DeathMessages.xml: " + ex.Message);
            }
        }
    }
}