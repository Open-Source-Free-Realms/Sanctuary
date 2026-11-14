using System.Numerics;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public sealed class CombatEncounterSpawn
{
    public int NpcId { get; init; }

    public Vector4 Position { get; init; }

    public float Heading { get; init; }
}
