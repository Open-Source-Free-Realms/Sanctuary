using System;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class EquipmentSlotDefinitionCollection : ObservableConcurrentDictionary<int, EquipmentSlotDefinition>
{
    private const int SlotNameColumn = 6;
    private const int AnimationPriorityColumn = 17;

    private readonly ILogger _logger;

    public EquipmentSlotDefinitionCollection(ILogger logger)
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
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            using var reader = Sep.New('^')
                .Reader()
                .From(fileStream);

            foreach (var row in reader)
            {
                if (row.ColCount <= AnimationPriorityColumn)
                    continue;

                var equipmentSlotDefinition = new EquipmentSlotDefinition();

                if (!int.TryParse(row[0].Span, out equipmentSlotDefinition.Id))
                    continue;

                equipmentSlotDefinition.SlotName = row[SlotNameColumn].ToString();

                if (!int.TryParse(row[AnimationPriorityColumn].Span, out equipmentSlotDefinition.AnimationPriority))
                    continue;

                if (!TryAdd(equipmentSlotDefinition.Id, equipmentSlotDefinition))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", equipmentSlotDefinition.Id, filePath);
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
