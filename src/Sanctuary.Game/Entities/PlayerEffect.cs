using System;
using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Entities;

public sealed class PlayerEffect
{
    public int Id { get; internal set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public int WorldEffectId { get; init; }
    public DateTimeOffset? WorldEffectStartsAt { get; init; }
    internal int WorldTagId;
    internal bool WorldEffectStarted;

    public int BuffIconId { get; init; }
    public int BuffNameId { get; init; }
    public int BuffDefinitionId { get; init; }

    public ulong SourceGuid { get; init; }

    public float MovementSpeedBonus { get; init; }

    public int AppearanceModelId { get; init; }
    public int AppearancePoofEffectId { get; init; }

    public float Scale { get; init; }

    /// <summary>
    /// Effects in the same group replace each other (see <see cref="Player.RemoveEffectGroup"/>).
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Added to the player's stats while the effect lasts.
    /// </summary>
    public IReadOnlyDictionary<CharacterStatId, float>? StatBonuses { get; init; }

    // Restores spread over time (potions that heal "over 5 seconds"), one tick per interval.
    public int HealthPerTick { get; init; }
    public int EnergyPerTick { get; init; }
    public int TickIntervalMs { get; init; }
    public int TickCount { get; init; }
    internal int TicksRemaining;
    internal DateTimeOffset NextTickAt;

    public Action? OnRemoved { get; init; }
}
