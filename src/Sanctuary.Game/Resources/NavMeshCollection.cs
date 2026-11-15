using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Microsoft.Extensions.Logging;

using DotRecast.Detour;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Pathing;

namespace Sanctuary.Game.Resources;


public class NavMeshCollection : ObservableConcurrentDictionary<string, DtNavMesh>
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, string> _filePaths = new();
    private readonly object _loadLock = new();

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

        Clear();
        _filePaths.Clear();

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.navmesh"))
            _filePaths[Path.GetFileNameWithoutExtension(filePath)] = filePath;

        _logger.LogInformation("Found {Count} navmesh(es) in \"{Path}\"; each loads when its zone is first created.", _filePaths.Count, directoryPath);
        return true;
    }

    public bool TryGetNavMesh(string zoneName, [MaybeNullWhen(false)] out DtNavMesh navMesh)
    {
        if (TryGetValue(zoneName, out navMesh))
            return true;

        if (!_filePaths.TryGetValue(zoneName, out var filePath))
            return false;

        lock (_loadLock)
        {
            if (TryGetValue(zoneName, out navMesh))
                return true;

            if (!NavMeshLoader.TryLoad(filePath, _logger, out var loadedNavMesh))
            {
                _logger.LogWarning(
                    "Skipping navmesh file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no navmesh pathfinding available.",
                    filePath, zoneName);

                _filePaths.TryRemove(zoneName, out _);
                return false;
            }

            navMesh = loadedNavMesh!;
            this[zoneName] = navMesh;

            _logger.LogInformation("Loaded navmesh for zone \"{ZoneName}\" from \"{Path}\".", zoneName, filePath);
            return true;
        }
    }
}
