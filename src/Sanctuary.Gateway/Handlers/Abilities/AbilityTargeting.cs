using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Handlers.Abilities;

public static class AbilityTargeting
{
    // Nearest other player to "from" within range, excluding excludeGuid if given.
    public static Player? FindNearestPlayer(IZone zone, Player from, float range, ulong excludeGuid = 0)
    {
        Player? nearest = null;
        var best2 = range * range;

        foreach (var candidate in zone.Players)
        {
            if (candidate.Guid == from.Guid || candidate.Guid == excludeGuid)
                continue;

            var dx = candidate.Position.X - from.Position.X;
            var dz = candidate.Position.Z - from.Position.Z;
            var d2 = dx * dx + dz * dz;
            if (d2 >= best2)
                continue;

            best2 = d2;
            nearest = candidate;
        }

        return nearest;
    }
}
