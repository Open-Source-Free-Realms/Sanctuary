using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sanctuary.Core.Helpers;

public static class GuidHelper
{
    private enum GuidType : byte
    {
        Player = 1,
        Npc = 2,
        Encounter = 0xE,

        Max = 15
    }

    private enum GuidPrefix : ushort
    {
        Guild = 0x07DD,
        House = 0x07F5,
        Fixture = House
    }

    private const int GuidBits = 64;
    private const int TypeBits = 4;
    private const ulong TypeMask = (1UL << TypeBits) - 1;

    private const int PrefixBits = 16;
    private const int PrefixShift = GuidBits - PrefixBits;

    private const int ServerIdBits = 4;
    private const int NpcInstanceIdShift = TypeBits + ServerIdBits;
    private const int NpcInstanceIdBits = 24;
    private const int NpcCounterShift = 36;
    private const int NpcCounterBits = 28;
    private const int FixedNpcCounterBits = 20;

    public const byte ServerId = 1;
    public const ulong OpenWorldInstanceId = 0;
    private const ulong FirstNpcInstanceId = OpenWorldInstanceId + 1;

    public const ulong MaxNpcCounter = (1UL << NpcCounterBits) - 1;
    public const ulong MaxNpcInstanceId = (1UL << NpcInstanceIdBits) - 1;
    public const ulong MaxFixedNpcCounter = (1UL << FixedNpcCounterBits) - 1;
    public const ulong MaxPrefixedId = (1UL << PrefixShift) - 1;

    private static ulong _encounterId;

    public static int GetNextEncounterId()
    {
        return checked((int)GetGuid(GuidType.Encounter, Interlocked.Increment(ref _encounterId)));
    }

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

    public static bool IsFixedNpcGuid(ulong guid)
    {
        return IsNpcGuid(guid) && GetNpcCounter(guid) <= MaxFixedNpcCounter;
    }

    public static ulong GetNpcInstanceId(ulong zoneId)
    {
        return (zoneId - FirstNpcInstanceId) % MaxNpcInstanceId + FirstNpcInstanceId;
    }

    public static ulong GetGuildGuid(ulong id)
    {
        return GetPrefixedGuid(GuidPrefix.Guild, id);
    }

    public static ulong GetGuildId(ulong guid)
    {
        return GetPrefixedId(GuidPrefix.Guild, guid);
    }

    public static bool IsGuildGuid(ulong guid)
    {
        return HasPrefix(GuidPrefix.Guild, guid);
    }

    public static ulong GetHouseGuid(ulong id)
    {
        return GetPrefixedGuid(GuidPrefix.House, id);
    }

    public static ulong GetHouseId(ulong guid)
    {
        return GetPrefixedId(GuidPrefix.House, guid);
    }

    public static ulong GetFixtureGuid(ulong id)
    {
        return GetPrefixedGuid(GuidPrefix.Fixture, id);
    }

    public static ulong GetFixtureId(ulong guid)
    {
        return GetPrefixedId(GuidPrefix.Fixture, guid);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasPrefix(GuidPrefix prefix, ulong guid)
    {
        return (guid >> PrefixShift) == (ushort)prefix;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetPrefixedGuid(GuidPrefix prefix, ulong id)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, MaxPrefixedId);

        return ((ulong)prefix << PrefixShift) | id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetPrefixedId(GuidPrefix prefix, ulong guid)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(guid >> PrefixShift, (ulong)(ushort)prefix);

        return guid & MaxPrefixedId;
    }
}