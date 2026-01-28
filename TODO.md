2026-01-28@09:50

Treba implementirati da poruke idu iz igre prema discordu i sa discorda u igru.
Na discordu treba paziti na blacklisted riječi; jer se ne smiju dozvoliti attachmenti, slanje linkova, slanje slika. Treba napraviti "moderaciju" i korisnik koji pošalje blacklisted poruku, na diskordu dobije opomenu. Nakon X opomena dobije mute na XX minuta. Ukoliko korisnik i nakon mute ponovno šalje blacklisted poruke nakon X puta; dobije isključenje sa kanala. X i XX je definirano u konfiguraciji kao i poruke za opomene, mute i isključenje sa kanala - ukoliko još nije, treba napraviti konfiguraciju. Sve se šalje kao log u admin kanal.

**Torch**: 1.3.1+  
**Space Engineers**: 1.208+  
**C#**: 4.6+ / .NET Framework 4.8

Updated TODO:

2026-01-16@09:20
Prvo bih trebalo srediti konfiguraciju, ukloniti duple zapise u više konfiguracijskih datoteka. Objediniti sve u MainConfig.cs što se tiće zajedničkih postavki konfiguracije, a u ostalima ostaviti što se tiće pojedinih modula. Sve bi se trebalo spremati i učitavati iz jedne konfiguracijske datoteke u xml obliku, spremanje i učitavanje po tagovima ovisno o modulu. 
Ovo ću staviti u novi todo.

old:
U konfiguraciji za Discord bot je postavljeno samo:
<?xml version="1.0"?>
<DiscordBotConfig xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <BotToken>YOUR_DISCORD_BOT_TOKEN</BotToken>
  <GuildID>0</GuildID>
  <BotPrefix>!</BotPrefix>
  <EnableDMNotifications>true</EnableDMNotifications>
  <VerificationCodeExpirationMinutes>15</VerificationCodeExpirationMinutes>
</DiscordBotConfig>

Nedostaje **konfiguracija** za:
* <CategorylId> - gdje će se kreirati forumi za fakcije po imenu fakcije iz SE igre - sjeti se da sam to bio spominjao.
* <ChatChannelId> - gdje će se slati informacije tko se spojio / odspojio sa servera, te death poruke. To bi bio i kanal koji se sinhronizira obostrano, na discordu se vide poruke koje su pisane u public chatu unutar igre i obrnuto.
Staviti boolean za uključen/isključen chat u jednom ili drugom smjeru:
  <BotToGame>true</BotToGame>
  <ServerToDiscord>true</ServerToDiscord>


Format poruka iz igre SE prema discordu:
  <Format>:rocket: **{p}**: {msg}</Format>
Format poruka iz discorda za SE:
  <Format2>[SE]{p}</Format2>

Format join/leave poruka:
  <Connect>:key: {p} connected to server</Connect>
  <Join>:sunny: {p} joined the server</Join>
  <Leave>:new_moon: {p} left the server</Leave>

Staff dio konfiguracije:
* <StaffLog> - gdje će se spremati informacije o kreiranim rolama, forumima i imenima korisnika na discordu
* <StatusChannelId> - gdje će se slati upozorenja o sim speed i kada se server podigne ili ugasi
Poruke u obliku:
  <Started>:white_check_mark: Server Started!</Started>
  <Stopped>:x: Server Stopped!</Stopped>
  <Restarted>:arrows_counterclockwise: Server Restarted!</Restarted>

Dodati i implementirati mogućnost za simping:
  <SimPing>true</SimPing>
  <SimChannel>ChannelID</SimChannel>
  <SimThresh>0.6</SimThresh>
  <SimMessage>@here Simulation speed has dropped below threshold!</SimMessage>
  <SimCooldown>1200</SimCooldown>
  <UseStatus>true</UseStatus>
  <StatusInterval>5000</StatusInterval>
  <StatusPre>Server Starting...</StatusPre>
  <Status>{p} players | SS {ss}</Status>

Novi feature:
Postoji li mogućnost prilikom kreiranja foruma, kreirati i voice kanal i kategoriju po imenu fakcije, gdje bi se slao chat unutar fakcije, te sinhronizirao obostrano: sa discorda u igru i iz igre na discord? Ako da, napraviti konfiguraciju:
  <UseFactionChat>true</UseFactionChat>
  <FacFormat>:ledger: **{p}**: {msg}</FacFormat>
  <FacFormat2>[SE-Fac]{p}</FacFormat2>

Također bi trebalo definirati boje:

  <GlobalColor>White</GlobalColor>
  <FacColor>Green</FacColor>
kada se piše sa discorda kako bi se razlikovalo ide li poruka u fakciju ili globalno.

Potrebno je staviti boolean u konfiguraciju za svaki modul što ovdje do sada nisam spomenuo, te isto implementirati na sve module. Sve boolean po defaultu isključiti, u konfiguraciji (i samom kodu) napraviti komentar za što se koristi koji parametar.


Probati implementirati kako radi SEDiscordBridge za ono što do sada nije spomenuto, ovdje je konfiguracija za ideju:

<?xml version="1.0" encoding="utf-8"?>
<SEDBConfig xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Enabled>true</Enabled>
  <PreLoad>true</PreLoad>
  <LoadRanks>true</LoadRanks>
  <Embed>true</Embed>
  <DisplaySteamId>true</DisplaySteamId>
  <BotToken>BOT_TOKEN</BotToken>
  <ChatChannelId>1400087647526966</ChatChannelId>
  <Format>:rocket: **{p}**: {msg}</Format>
  <Format2>[SE]{p}</Format2>
  <CommandChannelId>1400087647526966</CommandChannelId>
  <CommandPrefix>;;</CommandPrefix>
  <AsServer>false</AsServer>
  <UseNicks>true</UseNicks>
  <BotToGame>true</BotToGame>
  <ServerToDiscord>true</ServerToDiscord>
  <ServerName>server</ServerName>
  <StatusChannelId>1400087647526966</StatusChannelId>
  <Started>:white_check_mark: Server Started!</Started>
  <Stopped>:x: Server Stopped!</Stopped>
  <Restarted>:arrows_counterclockwise: Server Restarted!</Restarted>
  <StripGPS>false</StripGPS>
  <Connect>:key: {p} connected to server</Connect>
  <Join>:sunny: {p} joined the server</Join>
  <Leave>:new_moon: {p} left the server</Leave>
  <SimPing>true</SimPing>
  <SimChannel>14000687647526966</SimChannel>
  <SimThresh>0.6</SimThresh>
  <SimMessage>@here Simulation speed has dropped below threshold!</SimMessage>
  <SimCooldown>1200</SimCooldown>
  <UseStatus>true</UseStatus>
  <StatusInterval>5000</StatusInterval>
  <StatusPre>Server Starting...</StatusPre>
  <Status>{p} players | SS {ss}</Status>
  <MentOthers>true</MentOthers>
  <MentEveryone>false</MentEveryone>
  <TokenVisibleState>Visible</TokenVisibleState>
  <RemoveResponse>30</RemoveResponse>
  <FactionChannels />
  <GlobalColor>White</GlobalColor>
  <FacColor>Green</FacColor>
  <FacFormat>:ledger: **{p}**: {msg}</FacFormat>
  <FacFormat2>[SE-Fac]{p}</FacFormat2>
  <CommandPerms />
</SEDBConfig>

I ovdje je source kod na github:
https://github.com/FabioZumbi12/SEDiscordBridge
ili novija verzija:
https://github.com/Bishbash777/SEDB-RELOADED

--- 
Za BUG fix:
Provjeriti konfiguraciju, jer sam primjetio da se isti parametar 

        [XmlElement]
        public ulong GuildID { get; set; }

koristi u nekoliko datoteka:
Config\DiscordBotConfig.cs
Config\PluginConfig.cs

isto je i za MambaTorchDiscordSync.cfg, spominju se isti parametri kao i u DiscordBotConfig.cfg

Proći sve konfiguracijske datoteke i uskladiti parametre, ukloniti duple i staviti ih tamo gdje trebaju biti.

Trenutna verzija se spaja na discord i javlja ServerUP | SimSpeed - je li to realna ili hardkodirana SimSpeed?
Ne javlja na discord kada se spoji korisnik.
Ne šalje chat poruke
Ne reagira na komande...

Ovo je izvod iz torch loga:
13:01:18.5180 [INFO]   MultiplayerManagerBase: Player mamba joined (76561198020205461)
13:03:36.8840 [INFO]   Chat: [Faction:0] mamba: test fakcija
13:03:45.1858 [INFO]   Chat: [Global:0] mamba: globalni test
13:04:13.0529 [INFO]   Chat: [Global:0] mamba: radi?
13:05:16.1530 [INFO]   Chat: [Global:0] mamba: kreirana fakcija
13:05:26.0531 [INFO]   Chat: [Faction:216526902611792406] mamba: faction chat
13:05:59.6532 [INFO]   Chat: [Faction:216526902611792406] mamba: /tds help
13:06:10.4369 [INFO]   Chat: [Global:0] mamba: /tds help
13:07:34.4747 [INFO]   MultiplayerManagerBase: mamba (76561198020205461) Disconnected.
Ne prikazuje status: 
13:07:59.4178 [INFO]   Torch: Server stopped.

---
New feat:
Na novoj verziji napraviti GUI za uređivanje parametara preko torch servera, podjeljenu po tabovima ovisno o modulu

---
Plugin\MambaTorchDiscordSyncPlugin.cs
Postoji red sa:
Console.WriteLine($"║ mamba.TorchDiscordSync v2.0.0 - {title,-28}║");
Trebalo bi napraviti da dinamički čita iz manifest.xml koja je verzija aktivna, jer ako ćemo ispisivati na više mjesta verziju - kao sada ovdje - može doći do krivih informacija.
Molim ažurirati svugdje gdje se prikazuje verzija.
Istovremeno molim ažurirati sve komentare koji su na hrvatskom - da budu na engleskom.

