using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Opcode 98 - the "Take Me There" path family (client uses Kynapse pathfinding): ClientPathRequestPacket (sub 1) / ClientPathReplyPacket (sub 2).
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
