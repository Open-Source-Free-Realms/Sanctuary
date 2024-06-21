using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientUpdatePacketUpdateStat : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 7;

    public ulong Guid;

    public List<CharacterStat> Stats = new();

    public ClientUpdatePacketUpdateStat() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Stats);

        return writer.Buffer;
    }
}