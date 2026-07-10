using System.Globalization;
using System.Text.Json;
using DSharpPlus.Entities;
using Pelican_Keeper.Core;
using Pelican_Keeper.Models;
using Pelican_Keeper.Utilities;

namespace Pelican_Keeper.Discord;

/// <summary>
/// Manages persistent tracking of Discord message IDs for editing instead of re-creating.
/// </summary>
public static class LiveMessageStorage
{
    private static readonly object CacheLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string _historyFilePath = "MessageHistory.json";

    internal static LiveMessageJsonStorage? Cache { get; private set; }

    public enum TrackedMessageKind
    {
        Status,
        HostMetrics,
        Any
    }

    static LiveMessageStorage()
    {
        Cache = LoadAll();

        if (Cache?.LiveStore == null)
        {
            Logger.WriteLineWithStep("Failed to read MessageHistory.json. Initializing new in-memory cache.", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            Cache = new LiveMessageJsonStorage();
        }

        foreach (var id in Cache.LiveStore!)
            Logger.WriteLineWithStep($"Cached message ID: {id}", Logger.Step.MessageHistory);

        _ = ValidateCacheAsync();
    }

    /// <summary>
    /// Loads the message history from disk.
    /// </summary>
    public static LiveMessageJsonStorage? LoadAll(string? customPath = null)
    {
        var path = Configuration.FileManager.GetCustomFilePath("MessageHistory.json", customPath, silent: true);

        if (path == string.Empty)
        {
            Logger.WriteLineWithStep("MessageHistory.json not found. Creating default.", Logger.Step.MessageHistory);

            if (!string.IsNullOrEmpty(customPath))
            {
                Logger.WriteLineWithStep("Custom path specified but file not found.", Logger.Step.FileReading, Logger.OutputType.Error, shouldExit: true);
                return null;
            }

            File.WriteAllText("MessageHistory.json", JsonSerializer.Serialize(new LiveMessageJsonStorage(), JsonOptions));
            path = Configuration.FileManager.GetFilePath("MessageHistory.json", silent: true);

            if (path == string.Empty)
            {
                Logger.WriteLineWithStep("Unable to find MessageHistory.json after creation.", Logger.Step.FileReading, Logger.OutputType.Error, shouldExit: true);
                return null;
            }
        }

        try
        {
            var json = File.ReadAllText(path);
            _historyFilePath = path;
            Logger.WriteLineWithStep($"Loaded MessageHistory.json from: {path}", Logger.Step.MessageHistory);
            Cache = JsonSerializer.Deserialize<LiveMessageJsonStorage>(json) ?? new LiveMessageJsonStorage();
            NormalizeCache();
            return Cache;
        }
        catch (Exception ex)
        {
            Logger.WriteLineWithStep("Error loading message cache. Delete MessageHistory.json to recreate.", Logger.Step.MessageHistory, Logger.OutputType.Error, ex);
            Cache = new LiveMessageJsonStorage();
            NormalizeCache();
            return Cache;
        }
    }

    /// <summary>
    /// Saves a message ID to the cache (legacy non-paginated storage).
    /// </summary>
    public static void Save(ulong messageId)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            if (Cache!.LiveStore!.Contains(messageId)) return;

            Cache.LiveStore.Add(messageId);
            PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves a non-paginated message ID for a specific channel.
    /// </summary>
    public static void Save(DiscordChannel channel, ulong messageId)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            var key = ChannelKey(channel);
            var changed = false;

            if (!Cache!.LiveStore!.Contains(messageId))
            {
                Cache.LiveStore.Add(messageId);
                changed = true;
            }

            if (!Cache.ChannelLiveStore!.TryGetValue(key, out var existing) || existing != messageId)
            {
                Cache.ChannelLiveStore[key] = messageId;
                changed = true;
            }

            if (changed) PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves a per-server message ID for a specific channel and server.
    /// </summary>
    public static void SaveServer(DiscordChannel channel, string serverUuid, ulong messageId)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            var key = ServerKey(channel, serverUuid);
            var changed = false;

            if (!Cache!.LiveStore!.Contains(messageId))
            {
                Cache.LiveStore.Add(messageId);
                changed = true;
            }

            if (!Cache.ServerLiveStore!.TryGetValue(key, out var existing) || existing != messageId)
            {
                Cache.ServerLiveStore[key] = messageId;
                changed = true;
            }

            if (changed) PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves a paginated message with its current page index.
    /// </summary>
    public static void Save(ulong messageId, int pageIndex)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            if (Cache!.PaginatedLiveStore!.TryGetValue(messageId, out var existing) && existing == pageIndex) return;

            Cache.PaginatedLiveStore[messageId] = pageIndex;
            PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves a paginated message for a specific channel.
    /// </summary>
    public static void Save(DiscordChannel channel, ulong messageId, int pageIndex)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            var key = ChannelKey(channel);
            var changed = false;

            if (!Cache!.PaginatedLiveStore!.TryGetValue(messageId, out var existingPage) || existingPage != pageIndex)
            {
                Cache.PaginatedLiveStore[messageId] = pageIndex;
                changed = true;
            }

            if (!Cache.ChannelPaginatedLiveStore!.TryGetValue(key, out var existing)
                || existing.MessageId != messageId
                || existing.PageIndex != pageIndex)
            {
                Cache.ChannelPaginatedLiveStore[key] = new ChannelPaginatedMessage { MessageId = messageId, PageIndex = pageIndex };
                changed = true;
            }

            if (changed) PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves the host metrics message ID to the legacy cache.
    /// </summary>
    public static void SaveHostMetrics(ulong messageId)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            if (Cache!.HostMetricsMessageId == messageId) return;

            Cache.HostMetricsMessageId = messageId;
            PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Saves the host metrics message ID for a specific channel.
    /// </summary>
    public static void SaveHostMetrics(DiscordChannel channel, ulong messageId)
    {
        lock (CacheLock)
        {
            NormalizeCache();
            var key = ChannelKey(channel);
            var changed = false;

            if (Cache!.HostMetricsMessageId != messageId)
            {
                Cache.HostMetricsMessageId = messageId;
                changed = true;
            }

            if (!Cache.HostMetricsMessageIds!.TryGetValue(key, out var existing) || existing != messageId)
            {
                Cache.HostMetricsMessageIds[key] = messageId;
                changed = true;
            }

            if (changed) PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Removes a message ID from every cache shape.
    /// </summary>
    public static void Remove(ulong? messageId)
    {
        if (messageId == null) return;

        lock (CacheLock)
        {
            NormalizeCache();
            var id = messageId.Value;
            var removed = Cache!.LiveStore!.Remove(id);
            removed |= Cache.PaginatedLiveStore!.Remove(id);
            var channelLiveStore = Cache.ChannelLiveStore!;
            var serverLiveStore = Cache.ServerLiveStore!;
            var channelPaginatedLiveStore = Cache.ChannelPaginatedLiveStore!;
            var hostMetricsMessageIds = Cache.HostMetricsMessageIds!;

            foreach (var key in channelLiveStore.Where(kvp => kvp.Value == id).Select(kvp => kvp.Key).ToList())
            {
                channelLiveStore.Remove(key);
                removed = true;
            }

            foreach (var key in serverLiveStore.Where(kvp => kvp.Value == id).Select(kvp => kvp.Key).ToList())
            {
                serverLiveStore.Remove(key);
                removed = true;
            }

            foreach (var key in channelPaginatedLiveStore.Where(kvp => kvp.Value.MessageId == id).Select(kvp => kvp.Key).ToList())
            {
                channelPaginatedLiveStore.Remove(key);
                removed = true;
            }

            foreach (var key in hostMetricsMessageIds.Where(kvp => kvp.Value == id).Select(kvp => kvp.Key).ToList())
            {
                hostMetricsMessageIds.Remove(key);
                removed = true;
            }

            if (Cache.HostMetricsMessageId == id)
            {
                Cache.HostMetricsMessageId = null;
                removed = true;
            }

            if (removed) PersistCacheUnsafe();
        }
    }

    /// <summary>
    /// Gets a message ID if it exists in the non-paginated cache.
    /// </summary>
    public static ulong? Get(ulong? messageId)
    {
        if (messageId == null) return null;

        lock (CacheLock)
        {
            NormalizeCache();
            return Cache!.LiveStore!.Contains(messageId.Value) ? messageId : null;
        }
    }

    /// <summary>
    /// Gets the host metrics message ID if it exists and is still accessible in the provided channel.
    /// </summary>
    public static async Task<ulong?> GetExistingHostMetricsMessageAsync(DiscordChannel? channel)
    {
        if (channel == null) return null;

        var key = ChannelKey(channel);
        ulong? channelMessageId;
        ulong? legacyMessageId;

        lock (CacheLock)
        {
            NormalizeCache();
            channelMessageId = Cache!.HostMetricsMessageIds!.GetValueOrDefault(key);
            legacyMessageId = Cache.HostMetricsMessageId;
        }

        if (channelMessageId.HasValue && await MessageExistsAsync([channel], channelMessageId.Value))
            return channelMessageId;

        if (channelMessageId.HasValue)
            Remove(channelMessageId);

        if (legacyMessageId.HasValue && await MessageExistsAsync([channel], legacyMessageId.Value))
        {
            SaveHostMetrics(channel, legacyMessageId.Value);
            return legacyMessageId;
        }

        var scanned = await FindExistingMessageIdInChannelAsync(channel, TrackedMessageKind.HostMetrics, pruneDuplicates: true);
        if (scanned.HasValue)
        {
            SaveHostMetrics(channel, scanned.Value);
            return scanned.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets the page index for a paginated message.
    /// </summary>
    public static int? GetPaginated(ulong? messageId)
    {
        if (messageId == null) return null;

        lock (CacheLock)
        {
            NormalizeCache();
            return Cache!.PaginatedLiveStore!.TryGetValue(messageId.Value, out var index) ? index : null;
        }
    }

    /// <summary>
    /// Checks if a message exists in any of the specified channels.
    /// </summary>
    public static async Task<bool> MessageExistsAsync(List<DiscordChannel> channels, ulong messageId)
    {
        if (channels.Count == 0) return true;

        foreach (var channel in channels)
        {
            try
            {
                var msg = await channel.GetMessageAsync(messageId);
                if (msg != null) return true;
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                if (RuntimeContext.Config.Debug)
                    Logger.WriteLineWithStep($"Message {messageId} not found in #{channel.Name}", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            }
            catch (DSharpPlus.Exceptions.UnauthorizedException)
            {
                if (RuntimeContext.Config.Debug)
                    Logger.WriteLineWithStep($"No permission to read #{channel.Name}", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            }
            catch (DSharpPlus.Exceptions.BadRequestException ex)
            {
                if (RuntimeContext.Config.Debug)
                    Logger.WriteLineWithStep($"Bad request on #{channel.Name}: {ex.Message}", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            }
        }

        if (channels.Count > 1 && RuntimeContext.Config.Debug)
            Logger.WriteLineWithStep($"Message {messageId} not found in any channel.", Logger.Step.MessageHistory, Logger.OutputType.Error);

        return false;
    }

    private static async Task ValidateCacheAsync()
    {
        var channels = RuntimeContext.TargetChannels;
        if (channels.Count == 0) return;

        var changed = false;

        if (Cache?.LiveStore != null)
        {
            var valid = new HashSet<ulong>();
            foreach (var id in Cache.LiveStore.ToList())
            {
                if (await MessageExistsAsync(channels, id))
                    valid.Add(id);
                else
                    changed = true;
            }

            Cache.LiveStore = valid;
        }

        if (Cache?.PaginatedLiveStore != null)
        {
            foreach (var kvp in Cache.PaginatedLiveStore.ToList())
            {
                if (await MessageExistsAsync(channels, kvp.Key)) continue;
                Cache.PaginatedLiveStore.Remove(kvp.Key);
                changed = true;
            }
        }

        if (Cache?.ChannelLiveStore != null)
        {
            foreach (var kvp in Cache.ChannelLiveStore.ToList())
            {
                var channel = channels.FirstOrDefault(c => ChannelKey(c) == kvp.Key);
                if (channel == null || await MessageExistsAsync([channel], kvp.Value)) continue;
                Cache.ChannelLiveStore.Remove(kvp.Key);
                changed = true;
            }
        }

        if (Cache?.ServerLiveStore != null)
        {
            foreach (var kvp in Cache.ServerLiveStore.ToList())
            {
                var separatorIndex = kvp.Key.IndexOf(':', StringComparison.Ordinal);
                var channelKey = separatorIndex > 0 ? kvp.Key[..separatorIndex] : kvp.Key;
                var channel = channels.FirstOrDefault(c => ChannelKey(c) == channelKey);
                if (channel == null || await MessageExistsAsync([channel], kvp.Value)) continue;
                Cache.ServerLiveStore.Remove(kvp.Key);
                changed = true;
            }
        }

        if (Cache?.ChannelPaginatedLiveStore != null)
        {
            foreach (var kvp in Cache.ChannelPaginatedLiveStore.ToList())
            {
                var channel = channels.FirstOrDefault(c => ChannelKey(c) == kvp.Key);
                if (channel == null || await MessageExistsAsync([channel], kvp.Value.MessageId)) continue;
                Cache.ChannelPaginatedLiveStore.Remove(kvp.Key);
                changed = true;
            }
        }

        if (Cache?.HostMetricsMessageIds != null && RuntimeContext.HostMetricsChannel != null)
        {
            foreach (var kvp in Cache.HostMetricsMessageIds.ToList())
            {
                var channel = ChannelKey(RuntimeContext.HostMetricsChannel) == kvp.Key
                    ? RuntimeContext.HostMetricsChannel
                    : null;

                if (channel == null || await MessageExistsAsync([channel], kvp.Value)) continue;
                Cache.HostMetricsMessageIds.Remove(kvp.Key);
                changed = true;
            }
        }

        if (RuntimeContext.HostMetricsChannel != null && Cache?.HostMetricsMessageId != null)
        {
            var exists = await MessageExistsAsync([RuntimeContext.HostMetricsChannel], Cache.HostMetricsMessageId.Value);
            if (!exists)
            {
                Cache.HostMetricsMessageId = null;
                changed = true;
            }
        }

        if (changed) PersistCache();
    }

    private static void PersistCache()
    {
        lock (CacheLock)
        {
            NormalizeCache();
            PersistCacheUnsafe();
        }
    }

    private static void PersistCacheUnsafe()
    {
        var targetPath = Path.GetFullPath(_historyFilePath);
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory ?? ".", $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(Cache, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    /// <summary>
    /// Saves a message ID to the cache asynchronously (non-paginated).
    /// </summary>
    public static Task SaveAsync(ulong messageId)
    {
        Save(messageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves a message ID to the cache asynchronously for a specific channel.
    /// </summary>
    public static Task SaveAsync(DiscordChannel channel, ulong messageId)
    {
        Save(channel, messageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves a per-server message ID asynchronously for a specific channel and server.
    /// </summary>
    public static Task SaveServerAsync(DiscordChannel channel, string serverUuid, ulong messageId)
    {
        SaveServer(channel, serverUuid, messageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves a paginated message with its current page index asynchronously.
    /// </summary>
    public static Task SaveAsync(ulong messageId, int pageIndex)
    {
        Save(messageId, pageIndex);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves a paginated message with its current page index asynchronously for a specific channel.
    /// </summary>
    public static Task SaveAsync(DiscordChannel channel, ulong messageId, int pageIndex)
    {
        Save(channel, messageId, pageIndex);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets an existing message ID from the cache for a specific channel.
    /// </summary>
    public static async Task<ulong?> GetExistingMessageIdAsync(DiscordChannel channel)
    {
        var key = ChannelKey(channel);
        ulong? channelMessageId;
        List<ulong> legacyIds;

        lock (CacheLock)
        {
            NormalizeCache();
            channelMessageId = Cache!.ChannelLiveStore!.GetValueOrDefault(key);
            legacyIds = Cache.LiveStore!.ToList();
        }

        if (channelMessageId.HasValue && await MessageExistsAsync([channel], channelMessageId.Value))
            return channelMessageId;

        if (channelMessageId.HasValue)
            Remove(channelMessageId);

        foreach (var id in legacyIds)
        {
            if (!await MessageExistsAsync([channel], id)) continue;
            Save(channel, id);
            return id;
        }

        var scanned = await FindExistingMessageIdInChannelAsync(channel, TrackedMessageKind.Status);
        if (scanned.HasValue)
        {
            Save(channel, scanned.Value);
            return scanned.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets an existing per-server message ID from the cache for a specific channel and server.
    /// </summary>
    public static async Task<ulong?> GetExistingServerMessageIdAsync(DiscordChannel channel, string serverUuid, string serverName)
    {
        var key = ServerKey(channel, serverUuid);
        ulong? messageId;

        lock (CacheLock)
        {
            NormalizeCache();
            messageId = Cache!.ServerLiveStore!.GetValueOrDefault(key);
        }

        if (messageId.HasValue && await MessageExistsAsync([channel], messageId.Value))
            return messageId;

        if (messageId.HasValue)
            Remove(messageId);

        var scanned = await FindExistingMessageIdInChannelAsync(
            channel,
            TrackedMessageKind.Status,
            expectedTitle: serverName);

        if (scanned.HasValue)
        {
            SaveServer(channel, serverUuid, scanned.Value);
            return scanned.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets an existing paginated message with its page index for a specific channel.
    /// </summary>
    public static async Task<(ulong? messageId, int? pageIndex)> GetExistingPaginatedMessageAsync(DiscordChannel channel)
    {
        var key = ChannelKey(channel);
        ChannelPaginatedMessage? channelState;
        List<KeyValuePair<ulong, int>> legacyStates;

        lock (CacheLock)
        {
            NormalizeCache();
            Cache!.ChannelPaginatedLiveStore!.TryGetValue(key, out channelState);
            legacyStates = Cache.PaginatedLiveStore!.ToList();
        }

        if (channelState != null && await MessageExistsAsync([channel], channelState.MessageId))
            return (channelState.MessageId, channelState.PageIndex);

        if (channelState != null)
            Remove(channelState.MessageId);

        foreach (var kvp in legacyStates)
        {
            if (!await MessageExistsAsync([channel], kvp.Key)) continue;
            Save(channel, kvp.Key, kvp.Value);
            return (kvp.Key, kvp.Value);
        }

        var scanned = await FindExistingMessageIdInChannelAsync(channel, TrackedMessageKind.Status);
        if (scanned.HasValue)
        {
            Save(channel, scanned.Value, 0);
            return (scanned.Value, 0);
        }

        return (null, null);
    }

    /// <summary>
    /// Scans a channel for a recent Pelican Keeper message and returns its ID.
    /// </summary>
    public static Task<ulong?> FindExistingMessageIdInChannelAsync(DiscordChannel channel)
        => FindExistingMessageIdInChannelAsync(channel, TrackedMessageKind.Status);

    /// <summary>
    /// Scans a channel for a recent Pelican Keeper message and removes clear duplicates.
    /// </summary>
    public static async Task<ulong?> FindExistingMessageIdInChannelAsync(
        DiscordChannel channel,
        TrackedMessageKind kind,
        bool pruneDuplicates = false,
        string? expectedTitle = null)
    {
        try
        {
            var messages = await channel.GetMessagesAsync(100);
            var candidates = messages
                .Where(m => IsCandidateMessage(m, kind, expectedTitle))
                .ToList();

            var candidate = candidates.FirstOrDefault();
            if (candidate == null) return null;

            if (pruneDuplicates && candidates.Count > 1)
                await PruneDuplicateMessagesAsync(candidates.Skip(1));

            return candidate.Id;
        }
        catch (Exception ex)
        {
            if (RuntimeContext.Config.Debug)
                Logger.WriteLineWithStep($"Failed scanning #{channel.Name} for existing messages: {ex.Message}", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            return null;
        }
    }

    private static async Task PruneDuplicateMessagesAsync(IEnumerable<DiscordMessage> duplicates)
    {
        foreach (var duplicate in duplicates)
        {
            try
            {
                await duplicate.DeleteAsync();
                if (RuntimeContext.Config.Debug)
                    Logger.WriteLineWithStep($"Removed duplicate message {duplicate.Id}", Logger.Step.MessageHistory);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                // Already gone.
            }
            catch (Exception ex)
            {
                if (RuntimeContext.Config.Debug)
                    Logger.WriteLineWithStep($"Failed to remove duplicate message {duplicate.Id}: {ex.Message}", Logger.Step.MessageHistory, Logger.OutputType.Warning);
            }
        }
    }

    private static bool IsCandidateMessage(DiscordMessage message, TrackedMessageKind kind, string? expectedTitle = null)
    {
        if (message.Author?.IsBot != true || (message.Embeds?.Count ?? 0) == 0)
            return false;

        return message.Embeds!.Any(embed => kind switch
        {
            TrackedMessageKind.HostMetrics => IsHostMetricsEmbed(embed),
            TrackedMessageKind.Status => IsStatusEmbed(embed, expectedTitle),
            _ => IsHostMetricsEmbed(embed) || IsStatusEmbed(embed, expectedTitle)
        });
    }

    private static bool IsHostMetricsEmbed(DiscordEmbed embed)
    {
        var title = embed.Title ?? string.Empty;
        var footer = embed.Footer?.Text ?? string.Empty;

        return title.Contains("Host System Metrics", StringComparison.OrdinalIgnoreCase)
               || footer.Contains("Pelican Keeper Host", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStatusEmbed(DiscordEmbed embed, string? expectedTitle = null)
    {
        if (IsHostMetricsEmbed(embed)) return false;

        if (!string.IsNullOrWhiteSpace(expectedTitle)
            && !string.Equals(embed.Title?.Trim(), expectedTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        var footer = embed.Footer?.Text ?? string.Empty;
        var hasFooterMarker = footer.Contains("Pelican Keeper Status", StringComparison.OrdinalIgnoreCase)
                              || footer.Contains("Last Updated", StringComparison.OrdinalIgnoreCase);

        return hasFooterMarker && ((embed.Fields?.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(embed.Title));
    }

    private static void NormalizeCache()
    {
        Cache ??= new LiveMessageJsonStorage();
        Cache.LiveStore ??= [];
        Cache.ChannelLiveStore ??= [];
        Cache.ServerLiveStore ??= [];
        Cache.PaginatedLiveStore ??= [];
        Cache.ChannelPaginatedLiveStore ??= [];
        Cache.HostMetricsMessageIds ??= [];
    }

    private static string ChannelKey(DiscordChannel channel)
        => channel.Id.ToString(CultureInfo.InvariantCulture);

    private static string ServerKey(DiscordChannel channel, string serverUuid)
        => $"{ChannelKey(channel)}:{serverUuid}";
}
