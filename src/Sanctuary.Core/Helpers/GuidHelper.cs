using System;
using System.Runtime.CompilerServices;

namespace Sanctuary.Core.Helpers;

public static class GuidHelper
{
    private enum GuidType : byte
    {
        Player = 1,
        Npc = 2,
        House = 0xA,

        Max = 15
    }

    private const int GuidBits = 64;
    private const int TypeBits = 4;
    private const ulong TypeMask = (1UL << TypeBits) - 1;

    private const int ServerIdBits = 4;
    private const int NpcInstanceIdShift = TypeBits + ServerIdBits;
    private const int NpcInstanceIdBits = 24;
    private const int NpcCounterShift = 36;
    private const int NpcCounterBits = 28;

    public const byte ServerId = 1;
    public const ulong OpenWorldInstanceId = 0;

    public const ulong MaxNpcCounter = (1UL << NpcCounterBits) - 1;
    public const ulong MaxNpcInstanceId = (1UL << NpcInstanceIdBits) - 1;

    public static ulong GetPlayerGuid(ulong id)
    {
        return GetGuid(GuidType.Player, id);
    }

    public static ulong GetPlayerId(ulong guid)
    {
        return GetId(GuidType.Player, guid);
    }

    public static bool IsPlayerGuid(ulong guid)
    {
        return IsType(GuidType.Player, guid);
    }

    public static ulong GetHouseGuid(ulong id)
    {
        return GetGuid(GuidType.House, id);
    }

    public static ulong GetHouseId(ulong guid)
    {
        return GetId(GuidType.House, guid);
    }

    public static bool IsHouseGuid(ulong guid)
    {
        return IsType(GuidType.House, guid);
    }

    public static ulong GetNpcGuid(ulong counter, ulong instanceId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(counter, MaxNpcCounter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instanceId, MaxNpcInstanceId);

        var id = (counter << (NpcCounterShift - TypeBits))
            | (instanceId << (NpcInstanceIdShift - TypeBits))
            | ServerId;

        return GetGuid(GuidType.Npc, id);
    }

    public static ulong GetNpcCounter(ulong guid)
    {
        return GetId(GuidType.Npc, guid) >> (NpcCounterShift - TypeBits);
    }

    public static bool IsNpcGuid(ulong guid)
    {
        return IsType(GuidType.Npc, guid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsType(GuidType type, ulong guid)
    {
        return (guid & TypeMask) == (byte)type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetGuid(GuidType type, ulong id)
    {
        var low4Bits = (byte)type;

        ArgumentOutOfRangeException.ThrowIfGreaterThan(low4Bits, (byte)GuidType.Max);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, (1UL << (GuidBits - TypeBits)) - 1);

        return (id << TypeBits) | low4Bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetId(GuidType type, ulong guid)
    {
        var low4Bits = (byte)(guid & TypeMask);

        ArgumentOutOfRangeException.ThrowIfNotEqual(low4Bits, (byte)type);

        return guid >> TypeBits;
    }
}
