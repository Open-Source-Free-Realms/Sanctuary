using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Zones;

[DebuggerDisplay("{Longitude}, {Latitude}")]
public struct ZoneTile : IEquatable<ZoneTile>
{
    public int Longitude;
    public int Latitude;

    public List<ZoneTile> VisibleTiles;

    public ConcurrentDictionary<ulong, IEntity> Entities;

    public static readonly ZoneTile Empty = new(int.MinValue, int.MinValue);

    public ZoneTile(int longitude, int latitude)
    {
        Longitude = longitude;
        Latitude = latitude;

        Entities = [];
        VisibleTiles = [];
    }

    public override string ToString()
    {
        return $"{Longitude}, {Latitude}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Part1By1(int n)
    {
        n &= 0x0000ffff;
        n = (n | (n << 8)) & 0x00ff00ff;
        n = (n | (n << 4)) & 0x0f0f0f0f;
        n = (n | (n << 2)) & 0x33333333;
        n = (n | (n << 1)) & 0x55555555;
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EncodeMorton2(int x, int y)
    {
        return Part1By1(x) | Part1By1(y) << 1;
    }

    public static int GetHash(int longitude, int latitude)
    {
        return EncodeMorton2(longitude, latitude);
    }

    #region Equatable

    public bool Equals(ZoneTile other)
    {
        return Longitude == other.Longitude && Latitude == other.Latitude;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is ZoneTile other)
            return Equals(other);

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Longitude, Latitude);
    }

    public static bool operator ==(ZoneTile left, ZoneTile right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ZoneTile left, ZoneTile right)
    {
        return !(left == right);
    }

    #endregion
}