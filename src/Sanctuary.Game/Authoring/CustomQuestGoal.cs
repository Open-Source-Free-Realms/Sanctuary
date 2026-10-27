using System.Collections.Generic;
using System.Text.Json;

namespace Sanctuary.Game.Authoring;

/// <summary>
/// One goal of a GM-authored quest, stored as JSON in DbCustomQuest.Goals.
/// </summary>
public sealed class CustomQuestGoal
{
    public const string Talk = "talk";
    public const string Collect = "collect";
    public const string Reach = "reach";

    public string Type { get; set; } = Talk;

    /// <summary>
    /// talk: NPC definition id.
    /// </summary>
    public int NpcId { get; set; }

    /// <summary>
    /// collect: CollectionNodeTypes.json key and how many to gather.
    /// </summary>
    public string? NodeType { get; set; }
    public int Count { get; set; }

    /// <summary>
    /// reach: where to go.
    /// </summary>
    public int ZoneId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 8f;

    /// <summary>
    /// Free text shown to the player in chat when this goal starts; empty uses a generated line.
    /// </summary>
    public string? Text { get; set; }
}

public sealed class CustomQuestReward
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public static class CustomQuestJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<T> ReadList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Write<T>(List<T> items) => JsonSerializer.Serialize(items, Options);
}
