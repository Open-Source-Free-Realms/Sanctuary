using System;

namespace Sanctuary.Game.Entities;

public sealed class PlayerEffect
{
    public int Id { get; internal set; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public int WorldEffectId { get; init; }
    public DateTimeOffset? WorldEffectStartsAt { get; init; }
    internal int WorldTagId;
    internal bool WorldEffectStarted;

    public int BuffIconId { get; init; }
    public int BuffNameId { get; init; }

    public int AppearanceModelId { get; init; }
    public int AppearancePoofEffectId { get; init; }

    public float Scale { get; init; }

    public Action? OnRemoved { get; init; }
}
