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

# Set environment variables (or export them)
export BOT_TOKEN="your_discord_bot_token"
export SERVER_URL="https://panel.example.com"
export CLIENT_TOKEN="ptlc_your_client_key"
export SERVER_TOKEN="ptla_your_app_key"
export CHANNEL_IDS="1234567890123456789"

# Run
./"Pelican Keeper"
```

## Configuration

Configuration is done entirely via **environment variables** (no JSON files needed when running in containers).

### Required Environment Variables

- `BOT_TOKEN` - Discord bot token
- `SERVER_URL` - Pelican Panel URL (e.g., `https://panel.example.com`)
- `CLIENT_TOKEN` - Pelican client API key (`ptlc_...`)
- `SERVER_TOKEN` - Pelican application API key (`ptla_...`)
- `CHANNEL_IDS` - Discord channel ID (comma-separated for multiple channels)

### Optional Environment Variables

**Display Settings:**

- `MESSAGEFORMAT` - Display mode: `Consolidated`, `PerServer`, `Paginated` (default: `Consolidated`)
- `MESSAGESORTING` - Sort order: `Name`, `Players`, `Uptime` (default: `Name`)
- `MESSAGESORTINGDIRECTION` - Sort direction: `Ascending`, `Descending` (default: `Ascending`)
- `PLAYERCOUNTDISPLAY` - Enable game queries: `1`/`true` or `0`/`false`/`` (default: `true`)

**Filtering:**

- `IGNOREOFFLINESERVERS` - Hide offline servers: `1`/`0` (default: `false`)
- `IGNOREINTERNALSERVERS` - Hide internal IP servers: `1`/`0` (default: `false`)
- `SERVERSTOIGNORE` - Comma-separated server IDs to hide
- `SERVERSTOMONITOR` - If set, only show these server IDs (comma-separated)

**Auto-Shutdown:**

- `AUTOMATICSHUTDOWN` - Enable auto-shutdown: `1`/`0` (default: `false`)
- `SERVERSTOAUTOSHUTDOWN` - Server IDs to auto-shutdown (comma-separated)
- `EMPTYSERVERTIMEOUT` - Timeout before shutdown (default: `00:01:00`)

**Updates:**

- `AUTO_UPDATE` - Enable self-updating: `1`/`0` (default: `false`)
- `NOTIFYONUPDATE` - Notify on updates: `1`/`0` (default: `true`)
- `REPO_OWNER` - GitHub username for fork updates (default: `Sersium`)
- `REPO_NAME` - Repository name (default: `Pelican-Keeper`)

**Other:**

- `DEBUG` - Verbose logging: `1`/`0` (default: `false`)
- `NOTIFICATION_CHANNEL_ID` - Optional notification channel ID

**Note:** Pelican Panel toggle values use `1` for ON, empty string for OFF.

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
