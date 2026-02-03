# mamba.TorchDiscordSync v2.0

**NOTE: This project is still in active development.**  
Many features are partially working or require fixes.  
Not everything listed below is fully stable yet – ongoing work.

**Space Engineers Torch Server Plugin**  
Faction sync + death logging + chat bridge + server monitoring

**Author**: mamba  
**Version**: 2.0.0  
**Torch**: 1.3.1+  
**Space Engineers**: 1.208+  
**C#**: 4.6+ / .NET Framework 4.8  

## Features (Current Status)

| Feature                  | Status              | Notes                                                                 |
|--------------------------|---------------------|-----------------------------------------------------------------------|
| XML Database             | Completed           | Uses MainConfig.xml, DeathMessages.xml – auto-creates directories     |
| Chat Synchronization     | Completed           | Bidirectional global chat works; faction/private chat skipped         |
| Death Logging            | Partially Working   | Basic detection works; multiple deaths sometimes missed; killer/weapon "Unknown" |
| In-Game Death Announcements ("Death Messages") | In Progress | Basic message sent to global chat; location/zone not working correctly; still being fixed |
| Discord Death Notifications | In Progress     | One message per death (duplication fixed); location/zone not working correctly; uses templates |
| Server Monitoring (SimSpeed) | Partially Working | Always shows 1.00 (fallback) – real value not fetched                 |
| Faction Sync             | Disabled            | Timer off by default; enable in config to test                        |
| Admin Commands (/tds)    | Not Working         | None of the commands (/tds help, status, verify, sync, reset) are working yet |
| Security (SteamID whitelist) | Completed       | Admin commands restricted to whitelisted IDs (when they work)        |

## Known Issues

- In-Game & Discord death messages: location/zone not working correctly (still in progress)  
- Multiple deaths sometimes not logged or announced (only first one triggers)  
- SimSpeed always 1.00 (default fallback) – needs real API check  
- Faction sync timer disabled – enable in config if needed  
- Killer/weapon detection missing (shows "Unknown") – needs system chat parsing  
- Join/leave messages sometimes show SteamID instead of name (fixed in latest version)  
- Discord bot occasional disconnects (WebSocket closed)  
- **Admin commands completely non-functional** (/tds help, status, verify, sync, reset)  

## Installation

1. Download latest release or clone repo  
2. Extract .zip into Torch/Plugins/ folder  
3. Start server (auto-creates config in Instance/mambaTorchDiscordSync/)  
4. Edit Instance/mambaTorchDiscordSync/MambaTorchDiscordSync.cfg  
5. Restart server  

## Configuration

### MambaTorchDiscordSync.cfg

Main settings (XML format):

<Discord>
  <BotToken>YOUR_BOT_TOKEN</BotToken>
  <GuildID>000000000000</GuildID>
  <ChatChannelId>000000000000</ChatChannelId>
  <StaffLog>000000000000</StaffLog>
  <StatusChannelId>000000000000</StatusChannelId>
</Discord>

<Chat>
  <ServerToDiscord>true</ServerToDiscord>
  <GameToDiscordFormat>{p}: {msg}</GameToDiscordFormat>
</Chat>

<Death>
  <Enabled>true</Enabled>
  <AnnounceInGame>true</AnnounceInGame>
  <LogToDiscord>true</LogToDiscord>
</Death>

### DeathMessages.xml

Customize death messages with templates (placeholders {0}=killer, {1}=victim, {2}=weapon, {3}=location):

<FirstKill>
  <Message>🩸 FIRST BLOOD! {0} took their first victim - {1}</Message>
</FirstKill>

## Commands (Currently Not Working)

**In-game chat** (admin only – planned, but none work yet):

/tds sync     # Force faction sync  
/tds reset    # Clear Discord objects  
/tds status   # Show status  
/tds help     # Show help  
/tds verify   # Verification command  

## Troubleshooting

**Plugin not loading?**

- Check manifest.xml GUID  
- Verify Discord.Net packages  
- Check Torch logs  

**Death messages missing or incomplete?**

- Verify DeathMessages.xml syntax  
- Enable Debug mode  
- Check EventChannelDeathJoinLeave ID  

**Discord bot disconnects?**

- Verify bot token  
- Check bot permissions (Manage Roles, Send Messages, etc.)  

## License

MIT - See LICENSE file  

## Contributing

Pull requests welcome! Please use C# 4.6+ compatibility.

## Support

no support yet!