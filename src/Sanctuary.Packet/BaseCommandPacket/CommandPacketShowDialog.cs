using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client NPC conversation dialog (speech bubble + response buttons), rendered as HTML. Opcode 26 sub 3; wire layout traced from client deserializer FUN_00a9ef10, field offsets below are into the client's dialog struct.
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

    // +0x30 float: camera-focus zoom/blend param (FUN_008d2ba0) - 0 leaves framing broken, needs a real value.
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
