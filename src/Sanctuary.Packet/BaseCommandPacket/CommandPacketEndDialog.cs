using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketEndDialog : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public CommandPacketEndDialog() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        return writer.Buffer;
    }
}
