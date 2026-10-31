using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class ActivityDefinitionCollection
{
    private sealed class ActivityDefinitionFile
    {
        public List<ClientActivityDefinition> ZoneServer { get; set; } = [];
        public List<ClientActivityDefinition> WorldServer { get; set; } = [];
    }

    private readonly ILogger _logger;

    private Dictionary<int, ClientActivityDefinition> _activities = [];

    public IReadOnlyList<ClientActivityDefinition> ZoneServer { get; private set; } = [];
    public IReadOnlyList<ClientActivityDefinition> WorldServer { get; private set; } = [];

    public ActivityDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGet(int id, [MaybeNullWhen(false)] out ClientActivityDefinition clientActivityDefinition)
    {
        return _activities.TryGetValue(id, out clientActivityDefinition);
    }

    public bool TryGetByMiniGameId(int miniGameAppSystemId, int miniGameId, [MaybeNullWhen(false)] out ClientActivityDefinition clientActivityDefinition)
    {
        clientActivityDefinition = _activities.Values.FirstOrDefault(activity => activity.AppSystemId == miniGameAppSystemId && activity.AppSystemDataId == miniGameId);

        return clientActivityDefinition is not null;
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
                IncludeFields = true
            };

            var activityDefinitionFile = JsonSerializer.Deserialize<ActivityDefinitionFile>(fileStream, jsonSerializerOptions);

            if (activityDefinitionFile is null || (activityDefinitionFile.ZoneServer.Count == 0 && activityDefinitionFile.WorldServer.Count == 0))
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            ZoneServer = activityDefinitionFile.ZoneServer;
            WorldServer = activityDefinitionFile.WorldServer;
            _activities = ZoneServer.Concat(WorldServer).ToDictionary(clientActivityDefinition => clientActivityDefinition.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }
}
