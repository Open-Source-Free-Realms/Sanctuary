using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientUpdatePacketAddEffectTag : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 16;

    public EffectTag EffectTag = new();

    public ClientUpdatePacketAddEffectTag() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        EffectTag.Serialize(writer);

        return writer.Buffer;
    }
}
