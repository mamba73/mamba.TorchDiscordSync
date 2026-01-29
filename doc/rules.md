rules:
  - "Context: Space Engineers (SE) In-Game Scripting (Programmable Block)."
  - "Strictly use .NET Framework 4.6 and C# 6.0 syntax."
  - "API: Use VRage.Game, VRageMath, and Sandbox.ModAPI.Ingame namespaces."
  - "Constraint: No LINQ, no Reflection, no file access, no multithreading."
  - "Coding Style: Use string.Format() instead of interpolation ($)."
  - "Language: Respond in Croatian (ijekavica) or English. No Serbian/Ekavica."
  - "Comments: All code comments must be in ENGLISH."
  - "File Header: Mandatory! Always put the relative file path as a comment in the very first line of the code (e.g., // Scripts/Miner/RefinerControl.cs)."


Torch v1.3.1.328-master, SE 1.208.15
rules:
  - "Context: Space Engineers Torch Server Plugin development."
  - "Target: .NET Framework 4.8 or .NET 6+ (depending on Torch version)."
  - "API: Use Torch.API, VRage.Game, and Sandbox.ModAPI (Full API, not Ingame)."
  - "Capabilities: LINQ, Reflection, Multithreading, and File I/O ARE allowed."
  - "Language: Respond in Croatian (ijekavica) or English. No Serbian/Ekavica."
  - "Comments: All code comments must be in ENGLISH."
  - "File Header: Mandatory! Always put the relative file path as a comment in the very first line."
  - "Note: Ensure thread safety when interacting with the game world (MySandboxGame.Static.Invoke)."


rules:
  - "Context: Space Engineers (SE) World Modding (Script Mod)."
  - "Target: .NET Framework 4.8 (C# 7.3 or 8.0 is generally safe)."
  - "API: Use FULL Sandbox.ModAPI, VRage.Game.ModAPI, and Sandbox.Common.ObjectBuilders."
  - "Key Classes: Inherit from MySessionComponentBase or use [MyEntityComponentDescriptor]."
  - "Capabilities: LINQ and Reflection ARE allowed in Mods (unlike PB scripts)."
  - "Constraint: Still avoid direct File I/O or prohibited System calls to maintain Workshop compatibility."
  - "Language: Respond in Croatian (ijekavica) or English. No Serbian/Ekavica."
  - "Comments: All code comments must be in ENGLISH."
  - "File Header: Mandatory! Always put the relative file path as a comment in the first line."
  - "Important: Use MyAPIGateway for accessing game systems (Entities, Terminal, Session)."
