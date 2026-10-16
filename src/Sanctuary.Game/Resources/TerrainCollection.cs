using System.IO;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Terrain;

namespace Sanctuary.Game.Resources;


public class TerrainCollection : ObservableConcurrentDictionary<string, TerrainMesh>
{
    private readonly ILogger _logger;

    public TerrainCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation(
                "Terrain directory \"{Path}\" not found - no ground height data will be available.", directoryPath);
            return true;
        }

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.terrain"))
        {
            var zoneName = Path.GetFileNameWithoutExtension(filePath);

            if (!TerrainLoader.TryLoad(filePath, _logger, out var terrainMesh))
            {
                _logger.LogWarning(
                    "Skipping terrain file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no ground height data available.",
                    filePath, zoneName);
                continue;
            }

            this[zoneName] = terrainMesh!;
        }

        _logger.LogInformation("Loaded {Count} terrain mesh(es) from \"{Path}\".", Count, directoryPath);
        return true;
    }
}
