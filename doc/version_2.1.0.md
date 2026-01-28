 Analiziraj ovaj projekt:

https://github.com/mamba73/mamba.TorchDiscordSync2/tree/master

Ne diramo ništa što radi, bez "popravljanja" spremanja konfiguracije samo implementiramo i popravljamo postojeće po mojem odobrenju sukladno pravilima, koja ponavljam:

rules:
  - "Context: Space Engineers Torch Server Plugin development."
  - "Torch version: Torch v1.3.1.328-master, SE 1.208.15"
  - "Target: .NET Framework 4.8 or .NET 6+ (depending on Torch version)."
  - "API: Use Torch.API, VRage.Game, and Sandbox.ModAPI (Full API, not Ingame)."
  - "Capabilities: LINQ, Reflection, Multithreading, and File I/O ARE allowed."
  - "Language: Respond in Croatian (ijekavica) or English. No Serbian/Ekavica."
  - "Comments: All code comments must be in ENGLISH."
  - "File Header: Mandatory! Always put the relative file path as a comment in the very first line."
  - "Note: Ensure thread safety when interacting with the game world (MySandboxGame.Static.Invoke)."

Za sada ostajemo na XML, a pripremamo se tako da će kasnije kada sve proradi biti lako migrirati na SQLite. 

Ovo je "čista karta" za novi razgovor:

📋 Projektni sažetak: mamba.TorchDiscordSync

Status: Nastavak rada na postojećem projektu uz očuvanje funkcionalnih dijelova. Fokus je na modularnosti bez "ubijanja" koda koji radi.

🛠️ Tehnički parametri (Pravila)
Kontekst: Space Engineers Torch Server Plugin.
Verzije: Torch v1.3.1.328-master, SE 1.208.15.
Target: .NET Framework 4.8 (bez novije sintakse).

💀 Glavni zadatak: Death Messages ("Osmrtnice")
Proširiti OnPlayerDied.
Prikazati poruke na discordu o server statusu: "Server Up" i "Server Down" - koje su predefinirane u konfiguraciji
Analiza smrti: MyDamageInformation (DamageType, Amount, AttackerId).
Razlikovati: PvP, samoubojstvo, okoliš (kisik, pritisak) i gridove.

🚀 Cilj razgovora

Implementirati detaljne poruke o smrti bez diranja XML-a koji radi.
Uredi da se na discordu prikaže "server up" i "server down" 

### PRIORITETI

## 🔥 PRIORITET 1 – CORE STABILITY

SimSpeed ✔
Server up / down ✔
Join / leave ✔
Online player count ✔

## 🔥 PRIORITET 2 – CHAT 

Global chat → Discord ✔
Faction chat:
detektirati FACTION vs GLOBAL
konfiguracijski odlučiti:
ignorirati
slati u faction forum

➡️ Ovo je sljedeći veliki dobitak

## 🔥 PRIORITET 3 – FACTIONS (napravljena python skripta odlično radi taj posao, pa će biti uključena kao primjer kasnije)

Čitanje fakcija iz SE runtimea
Dodjela Discord rola
Spremanje promjena za undo

➡️ Ovdje C# mora prestati “glumiti bazu” i čitati SE

## 🔥 PRIORITET 4 – CLEANUP

Provjera postojećih featurea
Isključiti ili označiti sve što nije 100 % pouzdano

Kada sve proradi, na novoj verziji projekta prebaciti bazu podataka na SQLite