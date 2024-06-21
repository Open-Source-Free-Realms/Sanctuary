using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketSetEditMode : BaseHousingPacket, IDeserializable<ClientHousingPacketSetEditMode>
{
    public new const short OpCode = 6;

    public bool InEditMode;

    public ClientHousingPacketSetEditMode() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketSetEditMode value)
    {
        value = new ClientHousingPacketSetEditMode();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.InEditMode))
            return false;

        return reader.RemainingLength == 0;
    }
}