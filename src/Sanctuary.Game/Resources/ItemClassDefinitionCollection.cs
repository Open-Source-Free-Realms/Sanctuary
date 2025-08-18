using System;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class ItemClassDefinitionCollection : ObservableConcurrentDictionary<int, ItemClassDefinition>
{
    private readonly ILogger _logger;

    public ItemClassDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using var reader = Sep.New('^')
                .Reader()
                .From(fileStream);

            foreach (var row in reader)
            {
                if (row.ColCount < 6)
                    continue;

                var itemClassDefinition = new ItemClassDefinition();

                var column = 0;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.Id))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.NameId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.IconId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.WieldType))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.StatId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemClassDefinition.ProfileNameId))
                    continue;

                if (!TryAdd(itemClassDefinition.Id, itemClassDefinition))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", itemClassDefinition.Id, filePath);
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
}