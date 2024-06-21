using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Game.Resources;

public class QuickChatDefinitionCollection : ObservableConcurrentDictionary<int, QuickChatDefinition>
{
    private readonly ILogger _logger;

    public QuickChatDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (directory is null)
        {
            _logger.LogError("Invalid directory. \"{directory}\"", filePath);
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            var list = JsonSerializer.Deserialize<Dictionary<int, QuickChatDefinition>>(streamReader.ReadToEnd());

            if (list is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in list)
            {
                if (!TryAdd(entry.Key, entry.Value))
                {
                    _logger.LogWarning("Failed to add entry to dictionary. \"{entryId}\".", entry.Key);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }
}