using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: tears down an active NPC conversation dialog (CommandPacketShowDialog bubble) - unlike CommandPacketQuestDialogComplete (sub 29), it's a no-op against any other UI panel. No payload, header only (opcode 26 sub 4).
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
