using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: finalizes a quest NPC interaction after the player accepts.
// Traced from the client's BaseCommandPacket dispatcher (FUN_00aa2560): sub-opcode 29
// routes (via the case tables at 0xaa2950 / 0xaa290c, EDX=11) to handler FUN_00a99220,
// which recomputes the world camera from the current UI state (restoring normal follow-cam
// mode 1 when no full-screen panel is open) and then dispatches the Lua UI event
// "QuestStartHandler:DismissEndScreen" to tear down the quest start/end screen and restore
// the HUD.
// This is the packet the retail server sent on quest-accept. Unlike CommandPacketEndDialog
// (sub-opcode 4), which only tears down an active NPC *conversation* dialog object and is a
// no-op for the quest-offer cinematic camera, this handler unconditionally restores the
// camera - which is what unsticks the "camera frozen on the NPC after accepting" bug.
// The handler reads no payload; like CommandPacketEndDialog it must be exactly the 4-byte
// header (short OpCode 26 + short SubOpCode 29) with no trailing bytes.
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
