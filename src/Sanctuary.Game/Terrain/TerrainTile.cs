using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Terrain;

public sealed class TerrainTile
{
    private const int CellsPerSide = 8;
    private const double SeamTolerance = 0.05;
    private const double SeamMinUpFacing = 0.5;

    private const int CellCount = CellsPerSide * CellsPerSide;

    private readonly Vector3[] _vertices;
    private readonly ushort[]? _shortIndices;
    private readonly int[]? _indices;
    private readonly float _originX;
    private readonly float _originZ;
    private readonly float _cellSize;
    private readonly int[] _cellStarts;
    private readonly int[] _cellTriangles;

    public TerrainTile(Vector3[] vertices, List<int> indices, float originX, float originZ, float tileSize)
    {
        _vertices = vertices;
        _originX = originX;
        _originZ = originZ;
        _cellSize = tileSize / CellsPerSide;

        if (vertices.Length <= ushort.MaxValue + 1)
        {
            _shortIndices = new ushort[indices.Count];
            for (var index = 0; index < indices.Count; index++)
                _shortIndices[index] = (ushort)indices[index];
        }
        else
        {
            _indices = indices.ToArray();
        }

        var triangleCount = indices.Count / 3;

        _cellStarts = new int[CellCount + 1];
        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            GetCellBounds(triangle, out var minCellX, out var maxCellX, out var minCellZ, out var maxCellZ);

            for (var cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                for (var cellX = minCellX; cellX <= maxCellX; cellX++)
                    _cellStarts[cellZ * CellsPerSide + cellX + 1]++;
        }

        for (var cell = 0; cell < CellCount; cell++)
            _cellStarts[cell + 1] += _cellStarts[cell];

        _cellTriangles = new int[_cellStarts[CellCount]];
        var cellFill = _cellStarts[..CellCount];

        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            GetCellBounds(triangle, out var minCellX, out var maxCellX, out var minCellZ, out var maxCellZ);

            for (var cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                for (var cellX = minCellX; cellX <= maxCellX; cellX++)
                    _cellTriangles[cellFill[cellZ * CellsPerSide + cellX]++] = triangle;
        }
    }

    public bool TryGetHeight(float x, float z, float fromY, float maxDrop, out float height)
    {
        height = fromY - maxDrop;
        var found = false;

        var cell = GetCell(z - _originZ) * CellsPerSide + GetCell(x - _originX);

        foreach (var triangle in _cellTriangles.AsSpan(_cellStarts[cell], _cellStarts[cell + 1] - _cellStarts[cell]))
        {
            if (!TryGetTriangleHeight(triangle, x, z, out var triangleHeight))
                continue;

            if (triangleHeight > fromY || triangleHeight < height)
                continue;

            height = triangleHeight;
            found = true;
        }

        return found;
    }

    private int GetCell(float offset) => Math.Clamp((int)MathF.Floor(offset / _cellSize), 0, CellsPerSide - 1);

    private int GetIndex(int index) => _shortIndices is not null ? _shortIndices[index] : _indices![index];

    private void GetCellBounds(int triangle, out int minCellX, out int maxCellX, out int minCellZ, out int maxCellZ)
    {
        var a = _vertices[GetIndex(triangle * 3)];
        var b = _vertices[GetIndex(triangle * 3 + 1)];
        var c = _vertices[GetIndex(triangle * 3 + 2)];

        minCellX = GetCell(MathF.Min(a.X, MathF.Min(b.X, c.X)) - _originX);
        maxCellX = GetCell(MathF.Max(a.X, MathF.Max(b.X, c.X)) - _originX);
        minCellZ = GetCell(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) - _originZ);
        maxCellZ = GetCell(MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) - _originZ);
    }

    private bool TryGetTriangleHeight(int triangle, float x, float z, out float height)
    {
        var a = _vertices[GetIndex(triangle * 3)];
        var b = _vertices[GetIndex(triangle * 3 + 1)];
        var c = _vertices[GetIndex(triangle * 3 + 2)];

        var determinant = ((double)b.Z - c.Z) * ((double)a.X - c.X) + ((double)c.X - b.X) * ((double)a.Z - c.Z);
        if (Math.Abs(determinant) < double.Epsilon)
        {
            height = default;
            return false;
        }

        var weightA = (((double)b.Z - c.Z) * ((double)x - c.X) + ((double)c.X - b.X) * ((double)z - c.Z)) / determinant;
        var weightB = (((double)c.Z - a.Z) * ((double)x - c.X) + ((double)a.X - c.X) * ((double)z - c.Z)) / determinant;
        var weightC = 1d - weightA - weightB;

        var seam = GetUpFacing(a, b, c, determinant) >= SeamMinUpFacing ? SeamTolerance / Math.Abs(determinant) : 0d;

        if (weightA < -seam * GetFlatLength(b, c) || weightB < -seam * GetFlatLength(c, a) || weightC < -seam * GetFlatLength(a, b))
        {
            height = default;
            return false;
        }

        weightA = Math.Max(weightA, 0d);
        weightB = Math.Max(weightB, 0d);
        weightC = Math.Max(weightC, 0d);

        var weightSum = weightA + weightB + weightC;
        height = (float)((weightA * a.Y + weightB * b.Y + weightC * c.Y) / weightSum);
        return true;
    }

    private static double GetUpFacing(Vector3 a, Vector3 b, Vector3 c, double determinant)
    {
        var normal = Vector3.Cross(b - a, c - a);

        return Math.Abs(determinant) / normal.Length();
    }

    private static double GetFlatLength(Vector3 from, Vector3 to)
    {
        var x = (double)to.X - from.X;
        var z = (double)to.Z - from.Z;

        return Math.Sqrt(x * x + z * z);
    }
}
