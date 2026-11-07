using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPreviewByItemDefinitionIdRequest : BaseHousingPacket, IDeserializable<ClientHousingPacketPreviewByItemDefinitionIdRequest>
{
    public new const short OpCode = 20;

    public int ItemDefinitionId;

    public ClientHousingPacketPreviewByItemDefinitionIdRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPreviewByItemDefinitionIdRequest value)
    {
        value = new ClientHousingPacketPreviewByItemDefinitionIdRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ItemDefinitionId))
            return false;

        return reader.RemainingLength == 0;
    }
}