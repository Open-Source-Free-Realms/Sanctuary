using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketExecuteClientLua : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public int AbilityId;

    public AbilityPacketExecuteClientLua() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AbilityId);

        return writer.Buffer;
    }
}
