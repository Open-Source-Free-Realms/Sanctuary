using System;

using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class GameTimeHelper
{
    private const int ServerRate = 8;
    private const long GameTimeEpoch = 1221361670;

    public static PacketGameTimeSync GetGameTimeSyncPacket()
    {
        return new PacketGameTimeSync
        {
            Time = ServerRate * (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - GameTimeEpoch),
            ServerRate = ServerRate,
            UseClientTime = false
        };
    }
}
