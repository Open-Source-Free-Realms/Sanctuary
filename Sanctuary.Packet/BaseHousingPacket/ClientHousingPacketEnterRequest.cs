using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketEnterRequest : BaseHousingPacket, IDeserializable<ClientHousingPacketEnterRequest>
{
    public new const short OpCode = 19;

    public ulong HouseInstanceGuid;

    public int Unknown; // Set by EnterPreview

    public bool Unknown2;

    public ClientHousingPacketEnterRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketEnterRequest value)
    {
        value = new ClientHousingPacketEnterRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.HouseInstanceGuid))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}