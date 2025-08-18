namespace Sanctuary.Packet;

public class PacketDismountRequest : MountBasePacket
{
    public new const byte OpCode = 3;

    public PacketDismountRequest() : base(OpCode)
    {
    }
}