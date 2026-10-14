using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Terrain;

public sealed class TerrainMesh
{
    private const float WalkSampleSpacing = 0.5f;

    public required float TileSize { get; init; }
    public required IReadOnlyDictionary<(int X, int Z), TerrainTile> Tiles { get; init; }

    public bool TryGetGroundHeight(float x, float z, float fromY, float maxDrop, out float height)
    {
        var tileKey = ((int)MathF.Floor(x / TileSize), (int)MathF.Floor(z / TileSize));

        if (Tiles.TryGetValue(tileKey, out var tile))
            return tile.TryGetHeight(x, z, fromY, maxDrop, out height);

        height = default;
        return false;
    }

    public bool IsWalkable(Vector3 start, Vector3 goal, float maxStepUp, float maxStepDown, out float goalHeight)
    {
        goalHeight = start.Y;

        if (TryGetGroundHeight(start.X, start.Z, start.Y + maxStepUp, maxStepUp + maxStepDown, out var startHeight))
            goalHeight = startHeight;

        var flatDistance = MathF.Sqrt((goal.X - start.X) * (goal.X - start.X) + (goal.Z - start.Z) * (goal.Z - start.Z));
        var sampleCount = (int)MathF.Ceiling(flatDistance / WalkSampleSpacing);

        for (var sample = 1; sample <= sampleCount; sample++)
        {
            var amount = (float)sample / sampleCount;
            var x = start.X + (goal.X - start.X) * amount;
            var z = start.Z + (goal.Z - start.Z) * amount;

            if (!TryGetGroundHeight(x, z, goalHeight + maxStepUp, maxStepUp + maxStepDown, out goalHeight))
                return false;
        }

        return true;
    }
}
