using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Resources;

public static class NameFilterCollection
{
    public static bool TryLoad(string filePath, ILogger logger, out ICollection<string> blockedSubstrings)
    {
        blockedSubstrings = [];

        if (!File.Exists(filePath))
        {
            logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            var entries = File.ReadAllLines(filePath)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            blockedSubstrings = entries;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}
