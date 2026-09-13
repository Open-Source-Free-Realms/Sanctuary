using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientUpdatePacketAddEffectTag : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 16;

    public int TagId;

    public EffectTag EffectTag = new();

    public int IconId;
    public bool Unknown;
    public int NameId;
    public int Unknown2;

    public ClientUpdatePacketAddEffectTag() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TagId);

        using var tagWriter = new PacketWriter();

        EffectTag.Serialize(tagWriter);

        tagWriter.Write(IconId);
        tagWriter.Write(Unknown);
        tagWriter.Write(NameId);
        tagWriter.Write(Unknown2);

        writer.WritePayload(tagWriter.Buffer);

        return writer.Buffer;
    }
}
