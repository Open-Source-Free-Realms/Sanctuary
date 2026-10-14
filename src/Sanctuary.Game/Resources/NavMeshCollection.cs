using System.IO;

using Microsoft.Extensions.Logging;

using DotRecast.Detour;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Resources;


public class NavMeshCollection : ObservableConcurrentDictionary<string, DtNavMesh>
{
    private readonly ILogger _logger;

    public NavMeshCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation(
                "Navmesh directory \"{Path}\" not found - no navmesh pathfinding will be available.", directoryPath);
            return true;
        }

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.navmesh"))
        {
            var zoneName = Path.GetFileNameWithoutExtension(filePath);

            if (!NavMeshLoader.TryLoad(filePath, _logger, out var navMesh))
            {
                _logger.LogWarning(
                    "Skipping navmesh file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no navmesh pathfinding available.",
                    filePath, zoneName);
                continue;
            }

            this[zoneName] = navMesh!;
        }

        _logger.LogInformation("Loaded {Count} navmesh(es) from \"{Path}\".", Count, directoryPath);
        return true;
    }
}
