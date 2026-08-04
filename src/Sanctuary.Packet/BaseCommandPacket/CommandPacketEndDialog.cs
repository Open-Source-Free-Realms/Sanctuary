using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: tears down an active NPC *conversation* dialog object (a CommandPacketShowDialog
// bubble) - unlike CommandPacketQuestDialogComplete (sub 29), which is specific to the quest start/end
// screen and additionally dispatches "QuestStartHandler:DismissEndScreen", this is a no-op against any
// UI panel other than the conversation dialog itself. Opcode 26 sub 4 (BaseCommandPacket header).
// Like CommandPacketQuestDialogComplete, the handler reads no payload; must be exactly the 4-byte
// header (short OpCode 26 + short SubOpCode 4) with no trailing bytes.
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
