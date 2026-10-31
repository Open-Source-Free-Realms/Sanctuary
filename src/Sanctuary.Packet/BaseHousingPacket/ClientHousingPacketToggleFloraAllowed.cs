using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketToggleFloraAllowed : BaseHousingPacket, IDeserializable<ClientHousingPacketToggleFloraAllowed>
{
    public new const short OpCode = 12;

    public ClientHousingPacketToggleFloraAllowed() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketToggleFloraAllowed value)
    {
        value = new ClientHousingPacketToggleFloraAllowed();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
