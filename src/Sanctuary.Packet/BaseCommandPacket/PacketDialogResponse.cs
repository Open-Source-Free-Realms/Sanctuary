using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketDialogResponse : BaseCommandPacket, IDeserializable<PacketDialogResponse>
{
    public new const short OpCode = 6;

    public int ResponseId;

    public PacketDialogResponse() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketDialogResponse value)
    {
        value = new PacketDialogResponse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ResponseId))
            return false;

        return reader.RemainingLength == 0;
    }
}
