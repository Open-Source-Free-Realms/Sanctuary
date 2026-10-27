using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketQuestDialogComplete : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 29;

    public CommandPacketQuestDialogComplete() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        return writer.Buffer;
    }
}
