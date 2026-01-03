using System.Text.RegularExpressions;
using Pelican_Keeper.Core;
using Pelican_Keeper.Models;

namespace Pelican_Keeper.Utilities;

/// <summary>
/// Network and IP address utilities.
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Gets the default (primary) allocation for a server.
    /// </summary>
    public static ServerAllocation? GetDefaultAllocation(ServerInfo serverInfo)
    {
        if (serverInfo.Allocations == null || serverInfo.Allocations.Count == 0)
        {
            if (RuntimeContext.Config.Debug)
                Logger.WriteLineWithStep($"No allocations for server: {serverInfo.Name}", Logger.Step.Helper);
            return null;
        }

        return serverInfo.Allocations.FirstOrDefault(a => a.IsDefault) ?? serverInfo.Allocations.FirstOrDefault();
    }

    /// <summary>
    /// Determines the correct IP to display (internal or external).
    /// </summary>
    public static string GetDisplayIp(ServerInfo serverInfo)
    {
        var allocation = GetDefaultAllocation(serverInfo);
        if (allocation == null)
        {
            return "N/A";
        }

        if (!string.IsNullOrEmpty(RuntimeContext.Config.InternalIpStructure))
        {
            var pattern = "^" + Regex.Escape(RuntimeContext.Config.InternalIpStructure).Replace("\\*", "\\d+") + "$";
            if (Regex.IsMatch(allocation.Ip, pattern))
                return allocation.Ip;
        }

        return RuntimeContext.Secrets.ExternalServerIp ?? "0.0.0.0";
    }

    /// <summary>
    /// Gets formatted IP:Port string for display.
    /// </summary>
    public static string GetConnectAddress(ServerInfo serverInfo)
    {
        var allocation = GetDefaultAllocation(serverInfo);
        if (allocation == null)
        {
            return "N/A";
        }

        return $"{GetDisplayIp(serverInfo)}:{allocation.Port}";
    }

    /// <summary>
    /// Gets the IP to use for game server queries (internal allocation IP, not display IP).
    /// When allocation IP is 0.0.0.0, queries the Docker host gateway to reach host-published ports.
    /// </summary>
    public static string GetQueryIp(ServerInfo serverInfo)
    {
        var allocation = GetDefaultAllocation(serverInfo);
        if (allocation == null)
        {
            return "N/A";
        }

        // If allocation IP is empty, invalid
        if (string.IsNullOrEmpty(allocation.Ip))
        {
            return "N/A";
        }

        // If allocation IP is 0.0.0.0 (wildcard - server binding to all interfaces)
        // Wings publishes container ports to the host (e.g., -p 50050:50050)
        // From bot container, query the Docker host's gateway IP to reach these published ports
        if (allocation.Ip == "0.0.0.0")
        {
            // Try host.docker.internal first (Docker Desktop / newer Docker versions)
            // Otherwise use common Docker bridge gateway: 172.17.0.1
            return "host.docker.internal";
        }

        // Use the allocation IP for queries (internal Docker network)
        // Bot runs in Docker and queries game servers via internal network IPs
        return allocation.Ip;
    }

    /// <summary>
    /// Checks if an IP matches the internal network pattern.
    /// </summary>
    public static bool IsInternalIp(string ip)
    {
        if (string.IsNullOrEmpty(RuntimeContext.Config.InternalIpStructure))
            return false;

        var pattern = "^" + Regex.Escape(RuntimeContext.Config.InternalIpStructure).Replace("\\*", "\\d+") + "$";
        return Regex.IsMatch(ip, pattern);
    }
}
