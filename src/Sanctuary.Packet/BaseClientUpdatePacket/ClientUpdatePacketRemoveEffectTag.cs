using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketRemoveEffectTag : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public int EffectTagId;

    public ClientUpdatePacketRemoveEffectTag() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(EffectTagId);

        return writer.Buffer;
    }
}
