using System;
using System.IO;
using Sanctuary.Core.Collections;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class ModelDefinitionCollection : ObservableConcurrentDictionary<int, ModelDefinition>
{
    private readonly ILogger _logger;

    public ModelDefinitionCollection(ILogger logger)
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
                if (row.ColCount < 17)
                    continue;

                var modelDefinition = new ModelDefinition();

                var column = 0;

                if (!int.TryParse(row[column++].Span, out modelDefinition.Id))
                    continue;

                modelDefinition.ModelFileName = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out modelDefinition.RaceId))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.Scale))
                    continue;

                modelDefinition.Description = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out modelDefinition.Gender))
                    continue;

                if (!int.TryParse(row[column++].Span, out modelDefinition.Age))
                    continue;

                modelDefinition.Descriptor = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out modelDefinition.MaterialType))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.WaterDisplacement))
                    continue;

                if (!int.TryParse(row[column++].Span, out modelDefinition.TeleportEffectId))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.CameraDistance))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.CameraAngle))
                    continue;

                if (!int.TryParse(row[column++].Span, out var isValidForPC))
                    continue;

                modelDefinition.IsValidForPC = isValidForPC != 0;

                if (!float.TryParse(row[column++].Span, out modelDefinition.NamePlateOffset))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.CapsuleHeight))
                    continue;

                if (!float.TryParse(row[column++].Span, out modelDefinition.Radius))
                    continue;

                if (!TryAdd(modelDefinition.Id, modelDefinition))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", modelDefinition.Id, filePath);
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