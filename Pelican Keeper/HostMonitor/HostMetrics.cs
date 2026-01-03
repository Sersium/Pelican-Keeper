namespace Pelican_Keeper.HostMonitor;

/// <summary>
/// Represents current host system metrics from node-exporter.
/// </summary>
public class HostMetrics
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Cumulative idle seconds from node-exporter.
    /// </summary>
    public double CpuIdleSecondsTotal { get; set; }

    /// <summary>
    /// Cumulative total seconds from node-exporter.
    /// </summary>
    public double CpuTotalSecondsTotal { get; set; }

    /// <summary>
    /// Total system memory in bytes.
    /// </summary>
    public ulong MemoryTotalBytes { get; set; }

    /// <summary>
    /// Available system memory in bytes.
    /// </summary>
    public ulong MemoryAvailableBytes { get; set; }

    /// <summary>
    /// Memory used in bytes (Total - Available).
    /// </summary>
    public ulong MemoryUsedBytes => MemoryTotalBytes - MemoryAvailableBytes;

    /// <summary>
    /// Filesystem mount points with usage information.
    /// </summary>
    public List<DiskMount> Mounts { get; set; } = [];

    /// <summary>
    /// Whether the metrics were successfully fetched.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if fetching failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a filesystem mount point and its usage.
/// </summary>
public class DiskMount
{
    /// <summary>
    /// Mount point path (e.g., "/", "/home", "/mnt/storage").
    /// </summary>
    public required string MountPoint { get; set; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public ulong TotalBytes { get; set; }

    /// <summary>
    /// Available space in bytes.
    /// </summary>
    public ulong AvailableBytes { get; set; }

    /// <summary>
    /// Used space in bytes (Total - Available).
    /// </summary>
    public ulong UsedBytes => TotalBytes - AvailableBytes;

    /// <summary>
    /// Usage percentage (0-100).
    /// </summary>
    public double UsagePercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;

    /// <summary>
    /// Filesystem type (e.g., "ext4", "btrfs").
    /// </summary>
    public string? FilesystemType { get; set; }
}
