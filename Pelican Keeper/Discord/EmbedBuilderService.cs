using DSharpPlus.Entities;
using Pelican_Keeper.Core;
using Pelican_Keeper.Models;
using Pelican_Keeper.Utilities;
using Pelican_Keeper.HostMonitor;

namespace Pelican_Keeper.Discord;

/// <summary>
/// Builds Discord embeds for server status display.
/// </summary>
public class EmbedBuilderService
{
    /// <summary>
    /// Builds a single-server embed for PerServer display mode.
    /// </summary>
    public Task<DiscordEmbed> BuildSingleServerEmbedAsync(ServerInfo server)
    {
        var (message, serverName) = ServerMarkdownParser.ParseTemplate(server);

        var embed = new DiscordEmbedBuilder
        {
            Title = serverName,
            Color = DiscordColor.Azure
        };

        embed.AddField("\u200B", message, inline: true);

        if (RuntimeContext.Config.DryRun)
        {
            Logger.WriteLineWithStep(serverName, Logger.Step.EmbedBuilding);
            Logger.WriteLineWithStep(message, Logger.Step.EmbedBuilding);
        }

        embed.Footer = new DiscordEmbedBuilder.EmbedFooter
        {
            Text = $"Last Updated: {DateTime.Now:HH:mm:ss}"
        };

        if (RuntimeContext.Config.Debug)
        {
            Logger.WriteLineWithStep($"Embed character count: {GetEmbedCharacterCount(embed)}", Logger.Step.EmbedBuilding);
        }

        return Task.FromResult(embed.Build());
    }

    /// <summary>
    /// Builds a multi-server embed for Consolidated display mode.
    /// </summary>
    public Task<DiscordEmbed> BuildMultiServerEmbedAsync(List<ServerInfo> servers)
    {
        var embed = new DiscordEmbedBuilder
        {
            Color = DiscordColor.Azure
        };

        for (int i = 0; i < servers.Count && embed.Fields.Count < 25; i++)
        {
            var (message, serverName) = ServerMarkdownParser.ParseTemplate(servers[i]);
            embed.AddField(serverName, message, inline: true);

            if (RuntimeContext.Config.DryRun)
            {
                Logger.WriteLineWithStep(serverName, Logger.Step.EmbedBuilding);
                Logger.WriteLineWithStep(message, Logger.Step.EmbedBuilding);
            }
        }

        embed.Footer = new DiscordEmbedBuilder.EmbedFooter
        {
            Text = $"Last Updated: {DateTime.Now:HH:mm:ss}"
        };

        if (RuntimeContext.Config.Debug)
        {
            Logger.WriteLineWithStep($"Embed character count: {GetEmbedCharacterCount(embed)}", Logger.Step.EmbedBuilding);
        }

        return Task.FromResult(embed.Build());
    }

    /// <summary>
    /// Builds paginated embeds for Paginated display mode.
    /// </summary>
    public Task<List<DiscordEmbed>> BuildPaginatedEmbedsAsync(List<ServerInfo> servers)
    {
        var embeds = new List<DiscordEmbed>();

        foreach (var server in servers)
        {
            var (message, serverName) = ServerMarkdownParser.ParseTemplate(server);

            var embed = new DiscordEmbedBuilder
            {
                Title = serverName,
                Color = DiscordColor.Azure
            };

            embed.AddField("\u200B", message, true);

            if (RuntimeContext.Config.DryRun)
            {
                Logger.WriteLineWithStep(serverName, Logger.Step.EmbedBuilding);
                Logger.WriteLineWithStep(message, Logger.Step.EmbedBuilding);
            }

            embed.Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                Text = $"Last Updated: {DateTime.Now:HH:mm:ss}"
            };

            if (RuntimeContext.Config.Debug)
            {
                Logger.WriteLineWithStep($"Embed character count: {GetEmbedCharacterCount(embed)}", Logger.Step.EmbedBuilding);
            }

            embeds.Add(embed.Build());
        }

        return Task.FromResult(embeds);
    }

    /// <summary>
    /// Builds an embed displaying host system metrics.
    /// </summary>
    public Task<DiscordEmbed> BuildHostMetricsEmbedAsync(HostMetrics metrics)
    {
        var embed = new DiscordEmbedBuilder
        {
            Title = "🖥️ Host System Metrics",
            Color = DiscordColor.Cyan
        };

        if (!metrics.IsValid)
        {
            embed.AddField("Status", $"⚠️ Error: {metrics.ErrorMessage}", inline: false);
        }
        else
        {
            // CPU
            var cpuBar = BuildProgressBar(metrics.CpuUsagePercent);
            embed.AddField("CPU Usage", $"{cpuBar} {metrics.CpuUsagePercent:0.0}%", inline: false);

            // Memory
            var memUsedGb = metrics.MemoryUsedBytes / (1024.0 * 1024.0 * 1024.0);
            var memTotalGb = metrics.MemoryTotalBytes / (1024.0 * 1024.0 * 1024.0);
            var memPercent = (double)metrics.MemoryUsedBytes / metrics.MemoryTotalBytes * 100;
            var memBar = BuildProgressBar(memPercent);
            embed.AddField("Memory", $"{memBar} {memUsedGb:0.00} GB / {memTotalGb:0.00} GB ({memPercent:0.0}%)", inline: false);

            // Disk mounts
            if (metrics.Mounts.Count > 0)
            {
                var diskInfo = string.Join("\n", metrics.Mounts.Select(m =>
                {
                    var bar = BuildProgressBar(m.UsagePercent);
                    var usedStr = HostMetricsService.FormatBytes(m.UsedBytes);
                    var totalStr = HostMetricsService.FormatBytes(m.TotalBytes);
                    return $"{m.MountPoint}: {bar} {usedStr} / {totalStr} ({m.UsagePercent:0.0}%)";
                }));
                embed.AddField("Storage", diskInfo, inline: false);
            }
        }

        embed.Footer = new DiscordEmbedBuilder.EmbedFooter
        {
            Text = $"Last Updated: {DateTime.Now:HH:mm:ss}"
        };

        return Task.FromResult(embed.Build());
    }

    private static int GetEmbedCharacterCount(DiscordEmbedBuilder embed)
    {
        var count = 0;
        if (embed.Title != null) count += embed.Title.Length;
        if (embed.Description != null) count += embed.Description.Length;
        if (embed.Footer?.Text != null) count += embed.Footer.Text.Length;
        if (embed.Author?.Name != null) count += embed.Author.Name.Length;
        foreach (var field in embed.Fields)
        {
            count += field.Name?.Length ?? 0;
            count += field.Value?.Length ?? 0;
        }
        return count;
    }

    private static string BuildProgressBar(double percent, int width = 10)
    {
        var filled = (int)(percent / 100 * width);
        var empty = width - filled;
        var bar = new string('█', filled) + new string('░', empty);
        return $"[{bar}]";
    }
}
