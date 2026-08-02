using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: the NPC conversation dialog - a speech bubble that renders the NPC's dialogue
// (as HTML, so localization <font color> tags DO show, unlike the plain quest fields) with a
// list of response buttons ("You got it!", etc.). This is the retail "talk to an NPC" UI, distinct
// from the quest offer/end screens; we use it for mid-quest goal steps (e.g. Shakey advancing "Call
// the Crew!").
// Opcode 26 sub 3 (BaseCommandPacket header = short OpCode + short SubOpCode). Wire layout traced
// from the client deserializer FUN_00a9ef10 (reached via the 26/3 handler FUN_00aa12e0); the display
// is FUN_009f5ae0. Field offsets are into the client's dialog struct; read order on the wire is:
//   int   DialogueTextId   (+0x10) - resolved via Global.Text and shown as the bubble body
//   int   TitleTextId      (+0x14) - secondary text id (name/title); 0 = none
//   ulong NpcGuid          (+0x18/+0x1c) - the speaking NPC (portrait / camera focus)
//   bool  (+0x2c)
//   float (+0x30, NaN-checked)
//   response list          (+0x20, FUN_00a9d760): int Count, then Count nodes, each 5 ints
//                          (node+0x08, +0x0c, +0x10 = button label text id, +0x14, +0x18)
//   float[4] (+0x40) / float[4] (+0x50) / bool (+0x60) / float[4] (+0x70)
//   float (+0x80, NaN) / bool (+0x84) / bool (+0x85) / bool (+0x86) / float (+0x88, NaN) / int (+0x8c)
// The deserializer requires the buffer to be exactly consumed. Floats sent as 0 (not NaN).
public class CommandPacketShowDialog : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    // One response button. LabelTextId (node+0x10) is the Global.Text id
    // rendered on the button; the other ints identify/parametrize the response for the reply.
    public sealed class Response
    {
        public int Id;            // node+0x08 - response identifier
        public int ActionType;    // node+0x0c - response action/type
        public int LabelTextId;   // node+0x10 - button caption (Global.Text id), e.g. 103085 "You got it!"
        public int Param1;        // node+0x14
        public int Param2;        // node+0x18
    }

    public int DialogueTextId;    // +0x10 - the NPC's spoken line
    public int TitleTextId;       // +0x14
    public ulong NpcGuid;         // +0x18/+0x1c

    // +0x30 float: passed with NpcGuid to the camera-focus routine FUN_008d2ba0 (traced) - the
    // zoom/blend parameter for framing the speaking NPC. 0 left the framing broken; a real value
    // (e.g. 1.0) is needed. Exact scale TBD in-game.
    public float CameraFocusParam = 1f;

    public readonly List<Response> Responses = new();

    public CommandPacketShowDialog() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer); // short OpCode(26) + short SubOpCode(3)

        writer.Write(DialogueTextId);   // +0x10
        writer.Write(TitleTextId);      // +0x14
        writer.Write(NpcGuid);          // +0x18/+0x1c (8 bytes)
        writer.Write(false);            // +0x2c bool
        writer.Write(CameraFocusParam); // +0x30 float - camera focus zoom/blend on the NPC

        // Response-node list (+0x20): count-prefixed, 5 ints per node.
        writer.Write(Responses.Count);
        foreach (var response in Responses)
        {
            writer.Write(response.Id);          // node+0x08
            writer.Write(response.ActionType);  // node+0x0c
            writer.Write(response.LabelTextId); // node+0x10
            writer.Write(response.Param1);      // node+0x14
            writer.Write(response.Param2);      // node+0x18
        }

        // float[4] blocks + flags (all default/zero).
        for (int i = 0; i < 4; i++) writer.Write(0f); // +0x40
        for (int i = 0; i < 4; i++) writer.Write(0f); // +0x50
        writer.Write(false);                          // +0x60 bool
        for (int i = 0; i < 4; i++) writer.Write(0f); // +0x70
        writer.Write(0f);                             // +0x80 float
        writer.Write(false);                          // +0x84 bool
        writer.Write(false);                          // +0x85 bool
        writer.Write(false);                          // +0x86 bool
        writer.Write(0f);                             // +0x88 float
        writer.Write(0);                              // +0x8c int

        return writer.Buffer;
    }
}
