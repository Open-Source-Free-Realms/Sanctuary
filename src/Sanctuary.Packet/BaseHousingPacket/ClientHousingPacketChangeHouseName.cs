using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketChangeHouseName : BaseHousingPacket, IDeserializable<ClientHousingPacketChangeHouseName>
{
    public new const short OpCode = 9;

    public int NameId;

    public ClientHousingPacketChangeHouseName() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketChangeHouseName value)
    {
        value = new ClientHousingPacketChangeHouseName();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.NameId))
            return false;

        return reader.RemainingLength == 0;
    }
}