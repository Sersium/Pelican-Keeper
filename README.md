# Pelican Keeper

Discord status bot for Pelican Panel servers.

This fork is tuned for Docker/Pelican installs, reliable message updates, host metrics, and working player counts across Minecraft, Source/A2S, RCON, Bedrock, and Palworld-style servers.

## Features

- Server status embeds in consolidated, per-server, or paginated mode
- Discord start/stop controls
- Player counts through Minecraft ping, Bedrock ping, A2S, and RCON
- Host CPU, memory, and disk metrics through node-exporter
- Optional auto-shutdown for empty servers
- GitHub release auto-update support
- .NET 10 self-contained releases

## Pelican Install

1. Import `egg-pelican-keeper.json`.
2. Create a Pelican Keeper server.
3. Set the required variables.
4. Start the server.

Required variables:

- `BOT_TOKEN`
- `SERVER_URL`
- `CLIENT_TOKEN`
- `SERVER_TOKEN`
- `CHANNEL_IDS`

Useful optional variables:

- `REPO_OWNER=Sersium`
- `REPO_NAME=Pelican-Keeper`
- `AUTO_UPDATE=1`
- `NOTIFICATION_CHANNEL_ID=...`
- `HOST_METRICS_CHANNEL_ID=...`
- `NODE_EXPORTER_URL=http://node-exporter:9100/metrics`
- `HOST_METRICS_UPDATE_INTERVAL=3`
- `MessageFormat=Consolidated`
- `PlayerCountDisplay=1`
- `DEBUG=1`

Pelican toggles use `1` for on and empty/`0` for off.

## Game Queries

Edit `GamesToMonitor.json` to map eggs to protocols and variables. Supported protocols:

- `MinecraftJava`
- `MinecraftBedrock`
- `A2S`
- `Rcon`
- `Terraria`

For container-only games, prefer internal Docker DNS or variables:

- `QueryHost`
- `QueryHostVariable`
- `RconHost`
- `RconHostVariable`

The bot also tries sane fallbacks, including the Pelican server UUID as an internal Docker hostname.

## Development

```bash
dotnet restore
dotnet test --configuration Release
./build.sh linux
```

## Credits

Forked from [SirZeeno/Pelican-Keeper](https://github.com/SirZeeno/Pelican-Keeper).
