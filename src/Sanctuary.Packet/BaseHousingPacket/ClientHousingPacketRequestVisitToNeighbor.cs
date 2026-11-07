using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketRequestVisitToNeighbor : BaseHousingPacket, IDeserializable<ClientHousingPacketRequestVisitToNeighbor>
{
    public new const short OpCode = 21;

    public ulong Guid;

    public ClientHousingPacketRequestVisitToNeighbor() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRequestVisitToNeighbor value)
    {
        value = new ClientHousingPacketRequestVisitToNeighbor();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}