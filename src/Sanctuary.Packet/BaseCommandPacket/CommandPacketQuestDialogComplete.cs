using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: finalizes a quest NPC interaction after the player accepts (handler FUN_00a99220) - restores the follow-cam and dispatches "QuestStartHandler:DismissEndScreen", unsticking the "camera frozen on the NPC" bug. No payload, header only (opcode 26 sub 29).
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
