using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Terrain;

public static class TerrainLoader
{
    private const string Signature = "STRN";
    private const uint SupportedVersion = 2;
    private const float QuantizedMax = ushort.MaxValue;
    private const int ReadBufferSize = 1 << 16;

    private sealed class TileBuilder
    {
        public List<Vector3> Vertices { get; } = [];
        public List<int> Indices { get; } = [];
    }

    public static bool TryLoad(string filePath, ILogger logger, out TerrainMesh? terrainMesh)
    {
        terrainMesh = null;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            if (Encoding.ASCII.GetString(reader.ReadBytes(Signature.Length)) != Signature)
            {
                logger.LogError("Terrain file \"{Path}\" has an invalid signature.", filePath);
                return false;
            }

            var version = reader.ReadUInt32();
            if (version != SupportedVersion)
            {
                logger.LogError("Terrain file \"{Path}\" has unsupported version {Version}.", filePath, version);
                return false;
            }

            var tileSize = reader.ReadSingle();

            using var zLibStream = new ZLibStream(stream, CompressionMode.Decompress);
            using var bufferedStream = new BufferedStream(zLibStream, ReadBufferSize);
            using var tileReader = new BinaryReader(bufferedStream);

            var tileBuilders = new Dictionary<(int X, int Z), TileBuilder>();

            ReadTerrainTiles(tileReader, tileSize, tileBuilders);
            ReadPlacedColliders(tileReader, tileSize, tileBuilders);

            var tiles = new Dictionary<(int X, int Z), TerrainTile>(tileBuilders.Count);
            foreach (var (tileKey, tileBuilder) in tileBuilders)
                tiles[tileKey] = new TerrainTile(tileBuilder.Vertices.ToArray(), tileBuilder.Indices,
                    tileKey.X * tileSize, tileKey.Z * tileSize, tileSize);

            terrainMesh = new TerrainMesh { TileSize = tileSize, Tiles = tiles };
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            logger.LogError(exception, "Failed to load terrain file \"{Path}\".", filePath);
            return false;
        }
    }

    private static void ReadTerrainTiles(BinaryReader reader, float tileSize, Dictionary<(int X, int Z), TileBuilder> tileBuilders)
    {
        var tileCount = reader.ReadInt32();

        for (var tile = 0; tile < tileCount; tile++)
        {
            var tileKey = (reader.ReadInt32(), reader.ReadInt32());
            var originX = tileKey.Item1 * tileSize;
            var originZ = tileKey.Item2 * tileSize;
            var tileBuilder = GetTileBuilder(tileBuilders, tileKey);
            var firstVertex = tileBuilder.Vertices.Count;

            var vertexCount = reader.ReadInt32();
            tileBuilder.Vertices.EnsureCapacity(firstVertex + vertexCount);

            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                var x = originX + reader.ReadUInt16() / QuantizedMax * tileSize;
                var y = reader.ReadSingle();
                var z = originZ + reader.ReadUInt16() / QuantizedMax * tileSize;
                tileBuilder.Vertices.Add(new Vector3(x, y, z));
            }

            var indexCount = reader.ReadInt32();
            tileBuilder.Indices.EnsureCapacity(tileBuilder.Indices.Count + indexCount);

            for (var index = 0; index < indexCount; index++)
                tileBuilder.Indices.Add(firstVertex + reader.ReadUInt16());
        }
    }

    private static void ReadPlacedColliders(BinaryReader reader, float tileSize, Dictionary<(int X, int Z), TileBuilder> tileBuilders)
    {
        var meshes = new (Vector3[] Vertices, ushort[] Indices)[reader.ReadInt32()];
        for (var mesh = 0; mesh < meshes.Length; mesh++)
        {
            var vertices = new Vector3[reader.ReadInt32()];
            for (var vertex = 0; vertex < vertices.Length; vertex++)
                vertices[vertex] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            var indices = new ushort[reader.ReadInt32()];
            for (var index = 0; index < indices.Length; index++)
                indices[index] = reader.ReadUInt16();

            meshes[mesh] = (vertices, indices);
        }

        var placementCount = reader.ReadInt32();
        var placedVertices = new List<Vector3>();
        var tileVertexMaps = new Dictionary<(int X, int Z), int[]>();

        for (var placement = 0; placement < placementCount; placement++)
        {
            var mesh = meshes[reader.ReadInt32()];
            var right = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var up = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var forward = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var scale = reader.ReadSingle();

            placedVertices.Clear();
            foreach (var vertex in mesh.Vertices)
                placedVertices.Add(position + scale * (vertex.X * right + vertex.Y * up + vertex.Z * forward));

            tileVertexMaps.Clear();

            for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
            {
                var a = placedVertices[mesh.Indices[index]];
                var b = placedVertices[mesh.Indices[index + 1]];
                var c = placedVertices[mesh.Indices[index + 2]];

                var minTileX = (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X)) / tileSize);
                var maxTileX = (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X)) / tileSize);
                var minTileZ = (int)MathF.Floor(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) / tileSize);
                var maxTileZ = (int)MathF.Floor(MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) / tileSize);

                for (var tileZ = minTileZ; tileZ <= maxTileZ; tileZ++)
                {
                    for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                    {
                        var tileKey = (tileX, tileZ);
                        var tileBuilder = GetTileBuilder(tileBuilders, tileKey);

                        if (!tileVertexMaps.TryGetValue(tileKey, out var vertexMap))
                        {
                            vertexMap = new int[placedVertices.Count];
                            Array.Fill(vertexMap, -1);
                            tileVertexMaps[tileKey] = vertexMap;
                        }

                        for (var corner = 0; corner < 3; corner++)
                        {
                            var meshVertex = mesh.Indices[index + corner];

                            if (vertexMap[meshVertex] < 0)
                            {
                                vertexMap[meshVertex] = tileBuilder.Vertices.Count;
                                tileBuilder.Vertices.Add(placedVertices[meshVertex]);
                            }

                            tileBuilder.Indices.Add(vertexMap[meshVertex]);
                        }
                    }
                }
            }
        }
    }

    private static TileBuilder GetTileBuilder(Dictionary<(int X, int Z), TileBuilder> tileBuilders, (int X, int Z) tileKey)
    {
        if (!tileBuilders.TryGetValue(tileKey, out var tileBuilder))
        {
            tileBuilder = new TileBuilder();
            tileBuilders[tileKey] = tileBuilder;
        }

        return tileBuilder;
    }
}
