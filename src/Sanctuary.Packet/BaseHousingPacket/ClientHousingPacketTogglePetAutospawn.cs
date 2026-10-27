using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketTogglePetAutospawn : BaseHousingPacket, IDeserializable<ClientHousingPacketTogglePetAutospawn>
{
    public new const short OpCode = 13;

    public ClientHousingPacketTogglePetAutospawn() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketTogglePetAutospawn value)
    {
        value = new ClientHousingPacketTogglePetAutospawn();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}