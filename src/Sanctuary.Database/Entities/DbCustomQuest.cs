using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A GM-authored quest. Published ones are added to the live quest collection as quest
/// 900000 + <see cref="Id"/> (see Sanctuary.Game.Authoring.CustomQuestManager).
/// </summary>
public class DbCustomQuest
{
    public int Id { get; set; }

    /// <summary>
    /// Free text, sent to players in chat; the client's quest window shows <see cref="TitleId"/>.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Locale string ids shown in the client's quest window; 0 uses a generic string.
    /// </summary>
    public int TitleId { get; set; }
    public int DescriptionId { get; set; }

    /// <summary>
    /// NPC definition ids.
    /// </summary>
    public int GiverNpcId { get; set; }
    public int TurnInNpcId { get; set; }

    public int RewardCoins { get; set; }

    public int? PrerequisiteQuestId { get; set; }
    public int? NextQuestId { get; set; }

    /// <summary>
    /// JSON list of goals, in completion order.
    /// </summary>
    public string Goals { get; set; } = "[]";

    /// <summary>
    /// JSON list of item rewards.
    /// </summary>
    public string Rewards { get; set; } = "[]";

    public bool IsPublished { get; set; }

    public ulong? UpdatedBy { get; set; }

    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
}
