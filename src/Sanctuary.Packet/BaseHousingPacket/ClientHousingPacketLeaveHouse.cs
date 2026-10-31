using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketLeaveHouse : BaseHousingPacket, IDeserializable<ClientHousingPacketLeaveHouse>
{
    public new const short OpCode = 10;

    public ClientHousingPacketLeaveHouse() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketLeaveHouse value)
    {
        value = new ClientHousingPacketLeaveHouse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
