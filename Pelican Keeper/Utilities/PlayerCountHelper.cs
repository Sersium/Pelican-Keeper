using System.Text.RegularExpressions;

namespace Pelican_Keeper.Utilities;

/// <summary>
/// Utilities for parsing and formatting player count responses.
/// </summary>
public static class PlayerCountHelper
{
    /// <summary>
    /// Extracts player count from various server response formats.
    /// </summary>
    /// <param name="response">Raw server response string.</param>
    /// <param name="customPattern">Optional custom regex pattern.</param>
    /// <returns>Extracted player count, or 0 if not found.</returns>
    public static int ExtractPlayerCount(string? response, string? customPattern = null)
    {
        if (string.IsNullOrEmpty(response))
        {
            Logger.WriteLineWithStep("Empty server response for player count.", Logger.Step.Helper, Logger.OutputType.Error);
            return 0;
        }

        if (!response.Any(char.IsDigit))
            return 0;

        // Standard format: "5/20"
        var standard = Regex.Match(response, @"^(\d+)\/\d+$");
        if (standard.Success && int.TryParse(standard.Groups[1].Value, out var count))
        {
            return count;
        }

        // Ark RCON format: numbered player list
        var arkPlayers = Regex.Matches(response, @"(\d+)\.\s*([^,]+),\s*(.+)$", RegexOptions.Multiline);
        if (arkPlayers.Count > 0)
        {
            return arkPlayers.Count;
        }

        // Palworld format: CSV with header
        if (response.Contains("name,playeruid,steamid"))
        {
            var playerMatches = Regex.Matches(response, @"^(?!name,).+$", RegexOptions.Multiline);
            if (playerMatches.Count > 0)
            {
                return playerMatches.Count;
            }
        }

        // Factorio format: "Online players (X):"
        var factorio = Regex.Match(response, @"Online players \((\d+)\):");
        if (factorio.Success && int.TryParse(factorio.Groups[1].Value, out var factorioCount))
        {
            return factorioCount;
        }

        // Custom regex pattern
        if (!string.IsNullOrEmpty(customPattern))
        {
            var custom = Regex.Match(response, customPattern);
            if (custom.Success && int.TryParse(custom.Value, out var customCount))
            {
                return customCount;
            }
        }

        return 0;
    }

    /// <summary>
    /// Formats player count for display with max players.
    /// </summary>
    public static string FormatPlayerCount(string? response, int maxPlayers = 0)
    {
        if (string.IsNullOrEmpty(response) && maxPlayers > 0)
            return $"N/A/{maxPlayers}";

        if (string.IsNullOrEmpty(response))
            return "N/A";

        var maxDisplay = maxPlayers > 0 ? maxPlayers.ToString() : "Unknown";
        return $"{response}/{maxDisplay}";
    }
}
