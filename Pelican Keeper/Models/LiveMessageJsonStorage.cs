namespace Pelican_Keeper.Models;

/// <summary>
/// Persistent storage for tracking Discord message IDs.
/// </summary>
public class LiveMessageJsonStorage
{
    /// <summary>Message IDs for non-paginated displays.</summary>
    public HashSet<ulong>? LiveStore { get; set; } = [];

    /// <summary>Channel ID to message ID mapping for non-paginated displays.</summary>
    public Dictionary<string, ulong>? ChannelLiveStore { get; set; } = [];

    /// <summary>Channel/server key to message ID mapping for per-server displays.</summary>
    public Dictionary<string, ulong>? ServerLiveStore { get; set; } = [];

    /// <summary>Message ID to page index mapping for paginated displays.</summary>
    public Dictionary<ulong, int>? PaginatedLiveStore { get; set; } = [];

    /// <summary>Channel ID to paginated message state mapping.</summary>
    public Dictionary<string, ChannelPaginatedMessage>? ChannelPaginatedLiveStore { get; set; } = [];

    /// <summary>Message ID for the host metrics embed.</summary>
    public ulong? HostMetricsMessageId { get; set; }

    /// <summary>Channel ID to host metrics message ID mapping.</summary>
    public Dictionary<string, ulong>? HostMetricsMessageIds { get; set; } = [];
}

/// <summary>
/// Persistent paginated message state for a specific Discord channel.
/// </summary>
public class ChannelPaginatedMessage
{
    /// <summary>Discord message ID.</summary>
    public ulong MessageId { get; set; }

    /// <summary>Current page index.</summary>
    public int PageIndex { get; set; }
}
