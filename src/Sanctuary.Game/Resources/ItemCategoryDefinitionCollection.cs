using System;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Packet.Common;
using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources;

public class ItemCategoryDefinitionCollection : ObservableConcurrentDictionary<int, ItemCategoryDefinition>
{
    private readonly ILogger _logger;

    public ItemCategoryDefinitionCollection(ILogger logger)
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

                var itemCategoryDefinition = new ItemCategoryDefinition();

                var column = 0;

                if (!int.TryParse(row[column++].Span, out itemCategoryDefinition.Id))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemCategoryDefinition.NameId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemCategoryDefinition.IconId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemCategoryDefinition.Unknown))
                    continue;

                if (!int.TryParse(row[column++].Span, out var unknown2))
                    continue;

                itemCategoryDefinition.Unknown2 = unknown2 != 0;

                if (!int.TryParse(row[column++].Span, out itemCategoryDefinition.Unknown3))
                    continue;

                if (!TryAdd(itemCategoryDefinition.Id, itemCategoryDefinition))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", itemCategoryDefinition.Id, filePath);
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