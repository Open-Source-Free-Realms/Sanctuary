using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRemovePlayer : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 3;

    private short SubOpCode;

    public ulong Guid;

    public PlayerUpdatePacketRemovePlayer(short subOpCode = 0) : base(OpCode)
    {
        SubOpCode = subOpCode;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(SubOpCode);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public override void Write(PacketWriter writer)
    {
        base.Write(writer);

        writer.Write(SubOpCode);

        writer.Write(Guid);
    }
}