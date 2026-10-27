using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketToggleLocked : BaseHousingPacket, IDeserializable<ClientHousingPacketToggleLocked>
{
    public new const short OpCode = 11;

    public ClientHousingPacketToggleLocked() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketToggleLocked value)
    {
        value = new ClientHousingPacketToggleLocked();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}