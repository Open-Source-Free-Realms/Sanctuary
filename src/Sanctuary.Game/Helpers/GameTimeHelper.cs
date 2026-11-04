using System;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class GameTimeHelper
{
    private const int ServerRate = 8;
    private const long GameTimeEpoch = 1221361670;

    public static PacketGameTimeSync GetGameTimeSyncPacket(Player player)
    {
        return new PacketGameTimeSync
        {
            Time = ServerRate * (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - GameTimeEpoch) - player.TimezoneOffset,
            ServerRate = ServerRate,
            UseClientTime = false
        };
    }
}
