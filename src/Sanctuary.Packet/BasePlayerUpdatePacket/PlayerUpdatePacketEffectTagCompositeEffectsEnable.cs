using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketEffectTagCompositeEffectsEnable : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 43;

    public bool Enabled;

    public PlayerUpdatePacketEffectTagCompositeEffectsEnable() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Enabled);

        return writer.Buffer;
    }
}
