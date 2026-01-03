using Pelican_Keeper.Core;
using Pelican_Keeper.Utilities;

namespace Pelican_Keeper.HostMonitor;

/// <summary>
/// Service for managing host metrics retrieval and caching.
/// </summary>
public static class HostMetricsService
{
    private static HostMetrics _cachedMetrics = new();
    private static DateTime _lastFetchTime = DateTime.MinValue;
    private static readonly object LockObject = new();

    /// <summary>
    /// Gets current host metrics, updating from node-exporter if cache is stale.
    /// </summary>
    public static async Task<HostMetrics> GetMetricsAsync()
    {
        lock (LockObject)
        {
            // Return cached metrics if less than 1 second old
            if ((DateTime.Now - _lastFetchTime).TotalSeconds < 1)
                return _cachedMetrics;
        }

        var url = RuntimeContext.HostMetricsUrl ?? "http://node-exporter:9100/metrics";
        var metrics = await NodeExporterClient.FetchMetricsAsync(url);

        // Recompute CPU usage using deltas between samples for a realistic percentage
        lock (LockObject)
        {
            var prev = _cachedMetrics;
            if (metrics.IsValid && prev.CpuTotalSecondsTotal > 0 && metrics.CpuTotalSecondsTotal > prev.CpuTotalSecondsTotal)
            {
                var totalDelta = metrics.CpuTotalSecondsTotal - prev.CpuTotalSecondsTotal;
                var idleDelta = metrics.CpuIdleSecondsTotal - prev.CpuIdleSecondsTotal;
                if (totalDelta > 0)
                {
                    var usage = (1 - (idleDelta / totalDelta)) * 100;
                    metrics.CpuUsagePercent = Math.Clamp(usage, 0, 100);
                }
            }
        }

        lock (LockObject)
        {
            _cachedMetrics = metrics;
            _lastFetchTime = DateTime.Now;
        }

        return metrics;
    }

    /// <summary>
    /// Formats bytes to human-readable size string.
    /// </summary>
    public static string FormatBytes(ulong bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
