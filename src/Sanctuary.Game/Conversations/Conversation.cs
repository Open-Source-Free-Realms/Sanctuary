using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// A GM-authored dialog tree attached to a world NPC, stored as JSON in DbWorldNpc.Conversation.
/// </summary>
public sealed class Conversation
{
    public const int MaxNodes = 64;
    public const int MaxChoicesPerNode = 8;
    public const int MaxTextLength = 400;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public int Start { get; set; } = 1;

    public List<ConversationNode> Nodes { get; set; } = [];

    public ConversationNode? GetNode(int id) => Nodes.FirstOrDefault(x => x.Id == id);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Null for empty or invalid JSON.
    /// </summary>
    public static Conversation? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Conversation Clone() => FromJson(ToJson())!;
}

public sealed class ConversationNode
{
    public int Id { get; set; }

    /// <summary>
    /// What the NPC says.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    public List<ConversationChoice> Choices { get; set; } = [];
}

public sealed class ConversationChoice
{
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Node to go to; 0 ends the conversation.
    /// </summary>
    public int Next { get; set; }

    /// <summary>
    /// Item given when chosen; 0 for none.
    /// </summary>
    public int GiveItemId { get; set; }

    public int GiveQuantity { get; set; } = 1;

    /// <summary>
    /// Passed to the NPC script's onChoice(player, hook) when chosen.
    /// </summary>
    public string? Hook { get; set; }
}
