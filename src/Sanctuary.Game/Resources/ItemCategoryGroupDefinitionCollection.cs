using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class ItemCategoryGroupDefinitionCollection : ObservableConcurrentDictionary<int, List<ItemCategoryGroupDefinition>>
{
    private readonly ILogger _logger;

    public ItemCategoryGroupDefinitionCollection(ILogger logger)
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
                if (row.ColCount < 2)
                    continue;

                var itemCategoryGroupDefinition = new ItemCategoryGroupDefinition();

                var column = 0;

                if (!int.TryParse(row[column++].Span, out itemCategoryGroupDefinition.Id))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemCategoryGroupDefinition.CategoryId))
                    continue;

                if (TryGetValue(itemCategoryGroupDefinition.Id, out var list))
                {
                    list.Add(itemCategoryGroupDefinition);
                    continue;
                }

                if (!TryAdd(itemCategoryGroupDefinition.Id, [itemCategoryGroupDefinition]))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", itemCategoryGroupDefinition.Id, filePath);
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