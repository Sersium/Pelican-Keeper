# Pelican Keeper (Fork)

> **This is a fork of [SirZeeno/Pelican-Keeper](https://github.com/SirZeeno/Pelican-Keeper) with significant improvements and bug fixes.**

Discord bot for monitoring Pelican Panel game servers. Built with .NET 8.0.

## Why This Fork?

This fork focuses on reliability and production readiness:

- **Working Player Counts**: Fixed Minecraft queries with proper fallback mechanisms
- **Docker Compatibility**: Resolved networking issues for containerized deployments  
- **Stable Configuration**: Pelican Panel environment variable support
- **Better Testing**: Unit tests and CI/CD pipeline
- **Easy Deployment**: Build scripts and importable Pelican egg

## Features

- **Server Monitoring**: CPU, memory, disk, network, uptime tracking
- **Player Counts**: Query live player counts (Minecraft Java/Bedrock, A2S, RCON)
- **Display Modes**: Consolidated embed, per-server embeds, or paginated
- **Auto-Updates**: Self-updating binary with GitHub release integration
- **Server Control**: Start/stop servers via Discord buttons
- **Auto-Shutdown**: Automatically stop empty servers after timeout
- **Custom Templates**: Markdown-based embed customization

## Quick Start

### Install via Pelican Panel

1. Import the egg: `egg-pelican-keeper.json`
2. Create a new server with the egg
3. Configure environment variables:
   - `BOT_TOKEN` - Discord bot token
   - `SERVER_URL` - Pelican Panel URL
   - `CLIENT_TOKEN` - Client API key
   - `SERVER_TOKEN` - Application API key
   - `CHANNEL_IDS` - Discord channel ID
   - `REPO_OWNER` - Your GitHub username (for forks)
4. Start the server

### Manual Installation

```bash
# Download latest release
curl -L https://github.com/Sersium/Pelican-Keeper/releases/latest/download/Pelican-Keeper-v[VERSION]-linux-x64.zip -o pelican-keeper.zip
unzip pelican-keeper.zip
cd Pelican-Keeper-linux-x64

# Create Secrets.json (see Configuration below)
nano Secrets.json

# Run
./"Pelican Keeper"
```

## Configuration

### Secrets.json

```json
{
  "BotToken": "YOUR_DISCORD_BOT_TOKEN",
  "ServerUrl": "https://panel.example.com",
  "ClientToken": "ptlc_YOUR_CLIENT_API_KEY",
  "ServerToken": "ptla_YOUR_APPLICATION_API_KEY",
  "TargetChannels": [
    {
      "ChannelId": "1234567890123456789",
      "MessageId": null
    }
  ],
  "NotificationChannelId": null
}
```

### Config.json

Key settings:

- `MessageFormat`: `Consolidated` | `PerServer` | `Paginated`
- `PlayerCountDisplay`: `true` enables game server queries
- `AutomaticShutdown`: `true` stops empty servers after timeout
- `AutoUpdate`: `true` enables automatic updates
- `Debug`: `true` enables verbose logging

See [templates/](templates/) for example configurations.

## Game Server Queries

Supported protocols:

- **Minecraft Java**: Server List Ping (SLP) + mcstatus.io fallback
- **Minecraft Bedrock**: Bedrock query protocol
- **Source Games**: A2S (Valve Source Query)
- **Terraria**: RCON
- **Custom**: Extendable via `IQueryService`

Configure game types in `GamesToMonitor.json`.

## Development

### Build

```bash
./build.sh linux          # Linux x64
./build.sh windows        # Windows x64  
./build.sh all            # All platforms
```

### Test

```bash
dotnet test "Pelican Keeper.sln"
```

### Project Structure

```
Pelican Keeper/
├── Core/           # Context, configuration
├── Models/         # Data structures
├── Discord/        # Bot, embeds, interactions
├── Pelican/        # Panel API client
├── Query/          # Game server protocols
└── Utilities/      # Helpers, logging
```

## License

See [LICENSE](LICENSE) file for details.

## Credits

Original work by [SirZeeno](https://github.com/SirZeeno/Pelican-Keeper)
