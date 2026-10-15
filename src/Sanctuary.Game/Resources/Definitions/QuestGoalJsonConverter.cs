using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class QuestGoalJsonConverter : JsonConverter<QuestGoal>
{
    public override QuestGoal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        var goalType = QuestGoalType.TalkToNpc;

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, "Type", StringComparison.OrdinalIgnoreCase))
                continue;

            goalType = property.Value.ValueKind switch
            {
                JsonValueKind.Number => (QuestGoalType)property.Value.GetInt32(),
                JsonValueKind.String when Enum.TryParse<QuestGoalType>(property.Value.GetString(), true, out var parsed) => parsed,
                _ => throw new JsonException($"Unrecognised quest goal Type {property.Value}.")
            };

            break;
        }

        return goalType switch
        {
            QuestGoalType.TalkToNpc => element.Deserialize<TalkToNpcGoal>(options)!,
            QuestGoalType.ReachLocation => element.Deserialize<ReachLocationGoal>(options)!,
            QuestGoalType.Collect => element.Deserialize<CollectGoal>(options)!,
            _ => throw new JsonException($"Unknown quest goal Type {(int)goalType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, QuestGoal value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
