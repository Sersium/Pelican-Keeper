using System.Globalization;
using System.Text.RegularExpressions;
using Pelican_Keeper.Core;
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
            if (RuntimeContext.Config.Debug)
            {
                Logger.WriteLineWithStep($"node-exporter raw response:\n{response}", Logger.Step.Helper);
            }
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
        string[] allowedFsTypes = new[] { "overlay", "ext4", "xfs", "btrfs", "zfs", "apfs" };

        foreach (var line in lines)
        {
            // Skip comments
            if (line.StartsWith('#'))
                continue;

            // CPU metrics: sum all modes; track idle separately
            if (line.Contains("node_cpu_seconds_total"))
            {
                var match = Regex.Match(line, @"node_cpu_seconds_total\{[^}]*\}\s+([0-9.eE+-]+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    cpuTotal += value;
                    if (line.Contains("mode=\"idle\""))
                        cpuIdle += value;
                }
            }

            // Memory: node_memory_MemTotal_bytes value
            if (line.StartsWith("node_memory_MemTotal_bytes "))
            {
                var match = Regex.Match(line, @"node_memory_MemTotal_bytes\s+([0-9.eE+-]+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    metrics.MemoryTotalBytes = (ulong)Math.Max(0, value);
            }

            // Available memory: node_memory_MemAvailable_bytes value
            if (line.StartsWith("node_memory_MemAvailable_bytes "))
            {
                var match = Regex.Match(line, @"node_memory_MemAvailable_bytes\s+([0-9.eE+-]+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    metrics.MemoryAvailableBytes = (ulong)Math.Max(0, value);
            }

            // Filesystem: node_filesystem_size_bytes{device="...",fstype="...",mountpoint="..."} value
            if (line.Contains("node_filesystem_size_bytes{"))
            {
                var mountMatch = Regex.Match(line, @"mountpoint=""([^""]+)""");
                var fsMatch = Regex.Match(line, @"fstype=""([^""]+)""");
                var valueMatch = Regex.Match(line, @"node_filesystem_size_bytes\{[^}]+\}\s+([0-9.eE+-]+)");

                if (mountMatch.Success && valueMatch.Success && double.TryParse(valueMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    var mountpoint = mountMatch.Groups[1].Value;
                    var fsType = fsMatch.Success ? fsMatch.Groups[1].Value : string.Empty;

                    // Skip pseudo / ephemeral mounts and tiny bind mounts like /etc/hosts
                    if (mountpoint.StartsWith("/proc") || mountpoint.StartsWith("/sys") || mountpoint.StartsWith("/dev") || mountpoint.StartsWith("/run") || mountpoint.StartsWith("/etc/"))
                        continue;
                    if (allowedFsTypes.Length > 0 && !allowedFsTypes.Contains(fsType))
                        continue;

                    var bytes = (ulong)Math.Max(0, value);

                    if (!diskMounts.ContainsKey(mountpoint))
                    {
                        diskMounts[mountpoint] = new DiskMount
                        {
                            MountPoint = mountpoint,
                            TotalBytes = bytes,
                            FilesystemType = fsType
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
                var mountMatch = Regex.Match(line, "mountpoint=\"([^\\\"]+)\"");
                var valueMatch = Regex.Match(line, "node_filesystem_avail_bytes\\{[^}]+\\}\\s+([0-9.eE+-]+)");

                if (mountMatch.Success && valueMatch.Success && double.TryParse(valueMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    var mountpoint = mountMatch.Groups[1].Value;
                    if (!diskMounts.ContainsKey(mountpoint))
                    {
                        diskMounts[mountpoint] = new DiskMount { MountPoint = mountpoint };
                    }
                    diskMounts[mountpoint].AvailableBytes = (ulong)Math.Max(0, value);
                }
            }

            // Filesystem free: node_filesystem_free_bytes{mountpoint="..."} value (fallback if avail missing)
            if (line.Contains("node_filesystem_free_bytes{"))
            {
                var mountMatch = Regex.Match(line, "mountpoint=\"([^\\\"]+)\"");
                var valueMatch = Regex.Match(line, "node_filesystem_free_bytes\\{[^}]+\\}\\s+([0-9.eE+-]+)");

                if (mountMatch.Success && valueMatch.Success && double.TryParse(valueMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    var mountpoint = mountMatch.Groups[1].Value;
                    if (!diskMounts.ContainsKey(mountpoint))
                    {
                        diskMounts[mountpoint] = new DiskMount { MountPoint = mountpoint };
                    }

                    // Prefer avail if already set; otherwise use free
                    if (diskMounts[mountpoint].AvailableBytes == 0)
                        diskMounts[mountpoint].AvailableBytes = (ulong)Math.Max(0, value);
                }
            }
        }

        // Calculate CPU usage percentage using idle vs total time
        metrics.CpuIdleSecondsTotal = cpuIdle;
        metrics.CpuTotalSecondsTotal = cpuTotal;

        if (cpuTotal > 0)
        {
            var usage = (cpuTotal - cpuIdle) / cpuTotal * 100;
            metrics.CpuUsagePercent = Math.Clamp(usage, 0, 100);
        }
        else
        {
            metrics.IsValid = false;
            metrics.ErrorMessage = "No CPU metrics parsed";
        }

        // Add mounts, removing entries with zero sizes
        metrics.Mounts = diskMounts.Values
            .Where(m => m.TotalBytes > 0)
            .OrderBy(m => m.MountPoint)
            .ToList();

        // Mark invalid if critical parts missing
        if (metrics.MemoryTotalBytes == 0)
        {
            metrics.IsValid = false;
            metrics.ErrorMessage = metrics.ErrorMessage ?? "No memory metrics parsed";
        }
    }
}
