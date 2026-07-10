# Pelican Keeper

[![CI/CD](https://github.com/Sersium/Pelican-Keeper/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/Sersium/Pelican-Keeper/actions/workflows/dotnet.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker Ready](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](#docker-compose-install)
[![Pelican Egg](https://img.shields.io/badge/Pelican-egg-22c55e)](egg-pelican-keeper.json)
[![Prometheus Metrics](https://img.shields.io/badge/Prometheus-node--exporter-E6522C?logo=prometheus&logoColor=white)](#docker-compose-install)
[![Latest Release](https://img.shields.io/github/v/release/Sersium/Pelican-Keeper)](https://github.com/Sersium/Pelican-Keeper/releases/latest)
[![Release Downloads](https://img.shields.io/github/downloads/Sersium/Pelican-Keeper/total)](https://github.com/Sersium/Pelican-Keeper/releases)
[![Last Commit](https://img.shields.io/github/last-commit/Sersium/Pelican-Keeper)](https://github.com/Sersium/Pelican-Keeper/commits/main)
[![Repo Size](https://img.shields.io/github/repo-size/Sersium/Pelican-Keeper)](https://github.com/Sersium/Pelican-Keeper)
[![License](https://img.shields.io/github/license/Sersium/Pelican-Keeper)](LICENSE)

Discord status bot for Pelican Panel servers.

This fork is tuned for Docker/Pelican installs, reliable message updates, host resource monitoring, and player counts across Minecraft, Source/A2S, RCON, and Bedrock servers.

## Features

- Server status embeds in consolidated, per-server, or paginated mode
- Discord start/stop controls
- Player counts through Minecraft ping, Bedrock ping, A2S, and RCON
- Host CPU, memory, and disk metrics through a Prometheus-style node-exporter endpoint
- Optional auto-shutdown for empty servers
- GitHub release auto-update support
- .NET 10 self-contained releases

## Docker Compose Install

Use this when you want the bot outside Pelican as a normal portable Docker service.

```yaml
services:
  pelican-keeper:
    image: ghcr.io/parkervcp/yolks:dotnet_10
    working_dir: /app
    restart: unless-stopped
    command: ./"Pelican Keeper"
    volumes:
      - ./pelican-keeper:/app
    environment:
      BOT_TOKEN: ${BOT_TOKEN}
      SERVER_URL: https://panel.example.com
      CLIENT_TOKEN: ${CLIENT_TOKEN}
      SERVER_TOKEN: ${SERVER_TOKEN}
      CHANNEL_IDS: "123456789012345678"
      REPO_OWNER: Sersium
      REPO_NAME: Pelican-Keeper
      AUTO_UPDATE: "1"
      NODE_EXPORTER_URL: http://node-exporter:9100/metrics
    depends_on:
      - node-exporter

  node-exporter:
    image: quay.io/prometheus/node-exporter:latest
    restart: unless-stopped
    command:
      - --path.rootfs=/host
    volumes:
      - /:/host:ro,rslave
```

Initial setup:

```bash
mkdir -p pelican-keeper
LATEST="$(curl -fsSL https://api.github.com/repos/Sersium/Pelican-Keeper/releases/latest | sed -n 's/.*"tag_name": "\(v[^"]*\)".*/\1/p')"
curl -L "https://github.com/Sersium/Pelican-Keeper/releases/download/${LATEST}/Pelican-Keeper-${LATEST}-linux-x64.zip" -o pelican-keeper.zip
unzip -o pelican-keeper.zip -d pelican-keeper
docker compose up -d
```

Host resource monitoring needs node-exporter or another Prometheus-compatible `/metrics` endpoint. Set `HOST_METRICS_CHANNEL_ID` to enable the Discord host metrics embed.

## Pelican Egg Install

Use this when you want the bot managed as a Pelican server.

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
- `REPO_OWNER=Sersium`
- `REPO_NAME=Pelican-Keeper`

Useful optional variables:

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

Use these commands when changing the bot code locally.

```bash
dotnet restore
dotnet test --configuration Release
./build.sh linux
```

- `dotnet restore` downloads the NuGet packages the project needs.
- `dotnet test --configuration Release` builds the app and test project in Release mode, then runs the automated tests.
- `./build.sh linux` creates a Linux x64 self-contained build in `dist/`, similar to what the release workflow publishes.

You need the .NET 10 SDK installed. For README-only edits, you do not need to run the build commands.

## Credits

Forked from [SirZeeno/Pelican-Keeper](https://github.com/SirZeeno/Pelican-Keeper).
