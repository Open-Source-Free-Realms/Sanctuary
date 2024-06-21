using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRenamePlayer : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 19;

    public ulong Guid;

    public NameData Name = new();

    public PlayerUpdatePacketRenamePlayer() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        Name.Serialize(writer);

        return writer.Buffer;
    }
}