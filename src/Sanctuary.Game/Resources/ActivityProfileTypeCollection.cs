using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class ActivityProfileTypeCollection
{
    private readonly ILogger _logger;

    private ActivityProfileTypeDefinitions _definitions = new();

    public ActivityProfileTypeCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGet(ClientActivityDefinition clientActivityDefinition, MiniGameDefinition? miniGameDefinition, out ProfileType profileType)
    {
        var activityCategoryProfileTypeDefinition = _definitions.Categories
            .FirstOrDefault(definition => definition.Category == clientActivityDefinition.Category);

        if (activityCategoryProfileTypeDefinition is not null)
        {
            profileType = activityCategoryProfileTypeDefinition.ProfileType;
            return true;
        }

        var activityFilenameProfileTypeDefinition = _definitions.FilenameMarkers
            .FirstOrDefault(definition =>
                (clientActivityDefinition.DetailImageFilename?.Contains(definition.Marker, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (clientActivityDefinition.ThumbnailImageFilename?.Contains(definition.Marker, StringComparison.OrdinalIgnoreCase) ?? false));

        if (activityFilenameProfileTypeDefinition is not null)
        {
            profileType = activityFilenameProfileTypeDefinition.ProfileType;
            return true;
        }

        var miniGameTypeProfileTypeDefinition = miniGameDefinition is null
            ? null
            : _definitions.MiniGameTypes.FirstOrDefault(definition => definition.MiniGameType == miniGameDefinition.Type);

        profileType = miniGameTypeProfileTypeDefinition?.ProfileType ?? default;

        return miniGameTypeProfileTypeDefinition is not null;
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

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var definitions = JsonSerializer.Deserialize<ActivityProfileTypeDefinitions>(fileStream, jsonSerializerOptions);

            if (definitions is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            _definitions = definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }
}
