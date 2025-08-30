using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class StartInspectPacket : BaseInspectPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public ulong Guid;

    public bool ShowPedestal;

    public byte[] Payload = Array.Empty<byte>();

    public StartInspectPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(ShowPedestal);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}