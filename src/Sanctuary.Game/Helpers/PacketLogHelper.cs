using System;
using System.Buffers.Binary;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Traces a single player's gateway traffic while <see cref="Player.PacketLogger"/> is set.
/// </summary>
public static class PacketLogHelper
{
    // Tunnel header: short opcode, bool reliable, int payload size.
    private const int TunnelHeaderSize = sizeof(short) + sizeof(bool) + sizeof(int);

    public static void Log(ILogger logger, Player player, bool outgoing, ReadOnlySpan<byte> data)
    {
        logger.LogInformation("{Direction} {PlayerName} ({PlayerGuid}) {PacketName} ( Data: {Data} )",
            outgoing ? "S->C" : "C->S",
            player.Name,
            player.Guid,
            GetPacketName(data),
            Convert.ToHexString(data));
    }

    private static string GetPacketName(ReadOnlySpan<byte> data)
    {
        if (data.Length < sizeof(short))
            return "Unknown";

        try
        {
            var opCode = BinaryPrimitives.ReadInt16LittleEndian(data);

            if ((opCode == PacketTunneledClientPacket.OpCode || opCode == PacketTunneledClientWorldPacket.OpCode)
                && data.Length > TunnelHeaderSize)
            {
                var tunneledReader = new PacketReader(data[TunnelHeaderSize..]);
                return tunneledReader.ReadTunneledPacketName();
            }

            var reader = new PacketReader(data);
            return reader.ReadClientGatewayPacketName();
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }
}
