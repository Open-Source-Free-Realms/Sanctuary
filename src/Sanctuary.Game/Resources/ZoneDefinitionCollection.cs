using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class ZoneDefinitionCollection : ObservableConcurrentDictionary<int, ZoneDefinition>
{
    private readonly ILogger _logger;

    public ZoneDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath, string zoneFilePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var streamReader = new StreamReader(fileStream);

            var entries = JsonSerializer.Deserialize<List<ZoneDefinition>>(streamReader.ReadToEnd());

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in entries)
            {
                var gzneFilePath = Path.Combine(zoneFilePath, $"{entry.Name}.gzne");

                var gzne = LoadGzneDefinition(gzneFilePath);

                if (gzne is null)
                {
                    _logger.LogError("Failed to load gzne for zone \"{name}\".", entry.Name);
                    continue;
                }

                entry.Gzne = gzne;

                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.Id, filePath);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }

    private GzneDefinition? LoadGzneDefinition(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var gzneDefinition = new GzneDefinition();

        using var binaryReader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        var magic = new string(binaryReader.ReadChars(4));

        if (magic != "GZNE")
            return null;

        var version = binaryReader.ReadInt32();

        if (version > 3)
            return null;

        if (version >= 3)
            gzneDefinition.HideTerrain = (binaryReader.ReadInt32() & 1) == 1;

        gzneDefinition.ChunkSize = binaryReader.ReadInt32();
        gzneDefinition.TileSize = binaryReader.ReadInt32();

        gzneDefinition.WorldMin = binaryReader.ReadSingle();
        gzneDefinition.Unknown = binaryReader.ReadInt32();

        gzneDefinition.StartLongitude = binaryReader.ReadInt32();
        gzneDefinition.StartLatitude = binaryReader.ReadInt32();

        gzneDefinition.EndLongitude = binaryReader.ReadInt32();
        gzneDefinition.EndLatitude = binaryReader.ReadInt32();

        return gzneDefinition;
    }
}