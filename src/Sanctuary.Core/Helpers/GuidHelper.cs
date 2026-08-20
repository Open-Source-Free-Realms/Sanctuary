using System;
using System.Runtime.CompilerServices;

namespace Sanctuary.Core.Helpers;

public static class GuidHelper
{
    private enum GuidType : byte
    {
        Player = 1,
        House = 2,
        Fixture = 3,

        Max = 15
    }

    public static ulong GetPlayerGuid(ulong id)
    {
        return GetGuid(GuidType.Player, id);
    }

    public static ulong GetPlayerId(ulong guid)
    {
        return GetId(GuidType.Player, guid);
    }

    public static ulong GetHouseGuid(ulong id)
    {
        return GetGuid(GuidType.House, id);
    }

    public static ulong GetHouseId(ulong guid)
    {
        return GetId(GuidType.House, guid);
    }

    public static ulong GetFixtureGuid(ulong id)
    {
        return GetGuid(GuidType.Fixture, id);
    }

    public static ulong GetFixtureId(ulong guid)
    {
        return GetId(GuidType.Fixture, guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetGuid(GuidType type, ulong id)
    {
        var low4Bits = (byte)type;

        ArgumentOutOfRangeException.ThrowIfGreaterThan(low4Bits, 0x0F);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, (1UL << 60) - 1);

        return (id << 4) | low4Bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetId(GuidType type, ulong guid)
    {
        var low4Bits = (byte)(guid & 0x0F);

        ArgumentOutOfRangeException.ThrowIfNotEqual(low4Bits, (byte)type);

        return guid >> 4;
    }
}
