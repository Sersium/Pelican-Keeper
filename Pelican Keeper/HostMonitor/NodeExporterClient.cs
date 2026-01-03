using System.Text.RegularExpressions;
using Pelican_Keeper.Utilities;

namespace Pelican_Keeper.HostMonitor;

/// <summary>
/// Fetches and parses Prometheus metrics from node-exporter.
/// </summary>
public static class NodeExporterClient
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Fetches host metrics from node-exporter endpoint.
    /// </summary>
    /// <param name="url">Node-exporter metrics endpoint URL.</param>
    /// <returns>HostMetrics object with parsed data or error information.</returns>
    public static async Task<HostMetrics> FetchMetricsAsync(string url)
    {
        var metrics = new HostMetrics();

        try
        {
            var response = await Client.GetStringAsync(url);
            ParsePrometheusMetrics(response, metrics);
            metrics.IsValid = true;
        }
        catch (HttpRequestException ex)
        {
            metrics.IsValid = false;
            metrics.ErrorMessage = $"Connection failed: {ex.Message}";
            Logger.WriteLineWithStep($"Host metrics error: {metrics.ErrorMessage}", Logger.Step.Helper, Logger.OutputType.Error);
        }
        catch (TaskCanceledException)
        {
            metrics.IsValid = false;
            metrics.ErrorMessage = "Request timeout";
            Logger.WriteLineWithStep("Host metrics request timed out", Logger.Step.Helper, Logger.OutputType.Error);
        }
        catch (Exception ex)
        {
            metrics.IsValid = false;
            metrics.ErrorMessage = $"Parse error: {ex.Message}";
            Logger.WriteLineWithStep($"Host metrics parse error: {ex.Message}", Logger.Step.Helper, Logger.OutputType.Error);
        }

        return metrics;
    }

    /// <summary>
    /// Parses Prometheus text exposition format metrics.
    /// </summary>
    private static void ParsePrometheusMetrics(string response, HostMetrics metrics)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cpuIdle = 0.0;
        var cpuTotal = 0.0;
        var diskMounts = new Dictionary<string, DiskMount>();

        foreach (var line in lines)
        {
            // Skip comments
            if (line.StartsWith('#'))
                continue;

            // CPU metrics: node_cpu_seconds_total{mode="idle"} value
            if (line.Contains("node_cpu_seconds_total") && line.Contains("mode=\"idle\""))
            {
                var match = Regex.Match(line, @"node_cpu_seconds_total\{[^}]*\}\s+([\d.]+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var value))
                    cpuIdle += value;
            }
            else if (line.Contains("node_cpu_seconds_total"))
            {
                var match = Regex.Match(line, @"node_cpu_seconds_total\{[^}]*\}\s+([\d.]+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var value))
                    cpuTotal += value;
            }

            // Memory: node_memory_MemTotal_bytes value
            if (line.StartsWith("node_memory_MemTotal_bytes "))
            {
                var match = Regex.Match(line, @"node_memory_MemTotal_bytes\s+([\d.e+]+)");
                if (match.Success && ulong.TryParse(match.Groups[1].Value.Split('.')[0], out var bytes))
                    metrics.MemoryTotalBytes = bytes;
            }

            // Available memory: node_memory_MemAvailable_bytes value
            if (line.StartsWith("node_memory_MemAvailable_bytes "))
            {
                var match = Regex.Match(line, @"node_memory_MemAvailable_bytes\s+([\d.e+]+)");
                if (match.Success && ulong.TryParse(match.Groups[1].Value.Split('.')[0], out var bytes))
                    metrics.MemoryAvailableBytes = bytes;
            }

            // Filesystem: node_filesystem_size_bytes{device="...",fstype="...",mountpoint="..."} value
            if (line.Contains("node_filesystem_size_bytes{"))
            {
                var mountMatch = Regex.Match(line, @"mountpoint=""([^""]+)""");
                var fsMatch = Regex.Match(line, @"fstype=""([^""]+)""");
                var valueMatch = Regex.Match(line, @"node_filesystem_size_bytes\{[^}]+\}\s+([\d.e+]+)");

                if (mountMatch.Success && valueMatch.Success && ulong.TryParse(valueMatch.Groups[1].Value.Split('.')[0], out var bytes))
                {
                    var mountpoint = mountMatch.Groups[1].Value;
                    if (!diskMounts.ContainsKey(mountpoint))
                    {
                        diskMounts[mountpoint] = new DiskMount
                        {
                            MountPoint = mountpoint,
                            TotalBytes = bytes,
                            FilesystemType = fsMatch.Success ? fsMatch.Groups[1].Value : null
                        };
                    }
                    else
                    {
                        diskMounts[mountpoint].TotalBytes = bytes;
                    }
                }
            }

            // Filesystem available: node_filesystem_avail_bytes{mountpoint="..."} value
            if (line.Contains("node_filesystem_avail_bytes{"))
            {
                var mountMatch = Regex.Match(line, @"mountpoint=""([^""]+)""");
                var valueMatch = Regex.Match(line, @"node_filesystem_avail_bytes\{[^}]+\}\s+([\d.e+]+)");

                if (mountMatch.Success && valueMatch.Success && ulong.TryParse(valueMatch.Groups[1].Value.Split('.')[0], out var bytes))
                {
                    var mountpoint = mountMatch.Groups[1].Value;
                    if (!diskMounts.ContainsKey(mountpoint))
                    {
                        diskMounts[mountpoint] = new DiskMount { MountPoint = mountpoint };
                    }
                    diskMounts[mountpoint].AvailableBytes = bytes;
                }
            }
        }

        // Calculate CPU usage percentage
        if (cpuTotal > 0)
        {
            var usage = (cpuTotal - cpuIdle) / cpuTotal * 100;
            metrics.CpuUsagePercent = Math.Min(100, Math.Max(0, usage));
        }

        // Add mounts, sorted by mount point
        metrics.Mounts = diskMounts.Values.OrderBy(m => m.MountPoint).ToList();
    }
}
