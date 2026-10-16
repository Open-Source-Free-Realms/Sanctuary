using System;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Helpers;

public static class ResourceHelper
{
    public static void LoadDefinitions<T>(JsonElement definitions, JsonSerializerOptions jsonSerializerOptions,
        ILogger logger, string filePath, Func<T, int> getId, Func<T, bool> isValid, Func<int, T, bool> tryAdd) where T : class
    {
        if (definitions.ValueKind != JsonValueKind.Array)
        {
            logger.LogError("Invalid {type} definitions in file \"{file}\".", typeof(T).Name, filePath);
            return;
        }

        foreach (var definition in definitions.EnumerateArray())
        {
            try
            {
                var entry = definition.Deserialize<T>(jsonSerializerOptions);

                if (entry is null || !isValid(entry))
                {
                    logger.LogError("Invalid {type} definition in file \"{file}\". Entry={entry}",
                        typeof(T).Name, filePath, definition.GetRawText());
                    continue;
                }

                var id = getId(entry);

                if (!tryAdd(id, entry))
                    logger.LogWarning("Failed to add {type} entry. Id={id} \"{file}\"", typeof(T).Name, id, filePath);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse {type} definition in file \"{file}\". Entry={entry}",
                    typeof(T).Name, filePath, definition.GetRawText());
            }
        }
    }
}
