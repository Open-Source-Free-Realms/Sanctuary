using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientUpdatePacketActivateProfile : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 21;

    public byte[] Payload = Array.Empty<byte>();

    public List<CharacterAttachmentData> Attachments = new();

    public int Animation;
    public int CompositeEffect;

    private int Unused = default;

    public ClientUpdatePacketActivateProfile() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.WritePayload(Payload);

        writer.Write(Attachments);

        writer.Write(Animation);
        writer.Write(CompositeEffect);

        writer.Write(Unused);

        return writer.Buffer;
    }
}