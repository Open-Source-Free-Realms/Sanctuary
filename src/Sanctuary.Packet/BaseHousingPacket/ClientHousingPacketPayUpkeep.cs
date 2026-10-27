using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPayUpkeep : BaseHousingPacket, IDeserializable<ClientHousingPacketPayUpkeep>
{
    public new const short OpCode = 7;

    public ClientHousingPacketPayUpkeep() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPayUpkeep value)
    {
        value = new ClientHousingPacketPayUpkeep();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}