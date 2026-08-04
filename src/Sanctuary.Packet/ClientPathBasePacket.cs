using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Opcode 98 - the "Take Me There" path family (client uses Kynapse pathfinding). The client sends
// ClientPathRequestPacket (sub 1) when the button is clicked; the server replies with
// ClientPathReplyPacket (sub 2) carrying the path waypoints, which the client draws as the green
// breadcrumb trail and auto-walks along (processor FUN_009cd2f0 -> "Kynapse path attempt success").
public class ClientPathBasePacket
{
    public const short OpCode = 98;

    private byte SubOpCode;

    public ClientPathBasePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
