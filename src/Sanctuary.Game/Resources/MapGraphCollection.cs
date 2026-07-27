using System.IO;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Resources;

// Another "vibe-coded" file. Going to leave the basic parts of the summary here
// - Alko

/// <summary>
/// Loaded ".map" waypoint graphs, keyed by zone name (the filename
/// without extension - e.g. "bw_tanglewood_fort" from
/// "bw_tanglewood_fort.map").
/// </summary>
public class MapGraphCollection : ObservableConcurrentDictionary<string, MapGraph>
{
    private readonly ILogger _logger;

    public MapGraphCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation(
                "Maps directory \"{Path}\" not found - no .map pathfinding data will be available. " +
                "This is expected if no zones have .map files yet.", directoryPath);
            return true;
        }

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.map"))
        {
            var zoneName = Path.GetFileNameWithoutExtension(filePath);

            if (!MapGraphLoader.TryLoad(filePath, _logger, out var mapGraph))
            {
                _logger.LogWarning(
                    "Skipping map file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no pathfinding data available.",
                    filePath, zoneName);
                continue;
            }

            this[zoneName] = mapGraph!;
        }

        _logger.LogInformation("Loaded {Count} map graph(s) from \"{Path}\".", Count, directoryPath);
        return true;
    }
}