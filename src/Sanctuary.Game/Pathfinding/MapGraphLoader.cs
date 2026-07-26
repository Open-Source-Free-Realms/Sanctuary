using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Pathfinding;

/// <summary>
/// Loads the ".map" waypoint graph binary format (a native Free Realms
/// asset format, unrelated to Unity/ForgeLightToolkit).
///
/// File format (confirmed by clean full-file parses, zero leftover
/// bytes, on multiple real files): no header/magic bytes at all - just
/// repeating records back to back until EOF:
///
///     u32   node_id
///     f32   x, y, z            (world position)
///     u32   neighbor_count
///     neighbor_count x:
///         u32   neighbor_id
///         f32   distance
///
/// NOTE: `distance` was confirmed (on real files) to be exact
/// straight-line Euclidean distance between the two node positions.
/// This loader recomputes it from positions and warns on mismatch, as
/// both a parse sanity check and a guard against ever silently
/// trusting that assumption if it turns out false for some other file.
/// </summary>
public static class MapGraphLoader
{
    private const int RecordHeaderSize = 20; // u32 id + 3x f32 position + u32 neighbor_count
    private const int NeighborEntrySize = 8; // u32 neighbor_id + f32 distance
    private const int MaxSaneNeighborCount = 100;
    private const float DistanceMismatchTolerance = 0.5f;

    public static bool TryLoad(string filePath, ILogger logger, out MapGraph? mapGraph)
    {
        mapGraph = null;

        if (!File.Exists(filePath))
        {
            logger.LogError("Map file not found: \"{Path}\"", filePath);
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            logger.LogDebug("Loading Map \"{Path}\"...", filePath);

            var nodes = new Dictionary<int, MapNode>();

            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < RecordHeaderSize)
                {
                    logger.LogError(
                        "Map file \"{Path}\" is truncated: {Remaining} bytes left at offset {Offset}, " +
                        "not enough for a full record header.",
                        filePath, stream.Length - stream.Position, stream.Position);
                    return false;
                }

                var id = (int)reader.ReadUInt32();
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                var neighborCount = reader.ReadUInt32();

                if (neighborCount > MaxSaneNeighborCount)
                {
                    logger.LogError(
                        "Map file \"{Path}\" has a suspicious neighbor_count={Count} for node {Id} " +
                        "at offset {Offset} - likely a parse desync.",
                        filePath, neighborCount, id, stream.Position - 4);
                    return false;
                }

                var neighbors = new List<(int NeighborId, float Distance)>((int)neighborCount);

                for (var i = 0; i < neighborCount; i++)
                {
                    var neighborId = (int)reader.ReadUInt32();
                    var distance = reader.ReadSingle();
                    neighbors.Add((neighborId, distance));
                }

                nodes[id] = new MapNode
                {
                    Id = id,
                    Position = new Vector3(x, y, z),
                    Neighbors = neighbors
                };
            }

            var mismatchCount = 0;

            foreach (var node in nodes.Values)
            {
                foreach (var (neighborId, storedDistance) in node.Neighbors)
                {
                    if (!nodes.TryGetValue(neighborId, out var neighborNode))
                    {
                        logger.LogWarning(
                            "Map \"{Path}\": node {Id} references neighbor {NeighborId}, " +
                            "which doesn't exist in this file.", filePath, node.Id, neighborId);
                        continue;
                    }

                    var realDistance = Vector3.Distance(node.Position, neighborNode.Position);

                    if (MathF.Abs(realDistance - storedDistance) > DistanceMismatchTolerance)
                    {
                        mismatchCount++;
                        logger.LogWarning(
                            "Map \"{Path}\": node {Id} -> {NeighborId}: stored distance {Stored:F2} " +
                            "doesn't match real distance {Real:F2}",
                            filePath, node.Id, neighborId, storedDistance, realDistance);
                    }
                }
            }

            mapGraph = new MapGraph { Nodes = nodes };

            var totalEdges = 0;
            foreach (var node in nodes.Values)
                totalEdges += node.Neighbors.Count;

            logger.LogInformation(
                "Loaded Map \"{Path}\": {NodeCount} nodes, {EdgeCount} directed edges" +
                (mismatchCount > 0 ? ", {MismatchCount} distance mismatches (see warnings above)." : "."),
                filePath, nodes.Count, totalEdges, mismatchCount);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Map file \"{Path}\".", filePath);
            mapGraph = null;
            return false;
        }
    }
}