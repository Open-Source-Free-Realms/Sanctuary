using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketPlayerTitle : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 40;

    public PlayerTitleData Title = new();

    public ulong Guid;

    public PlayerUpdatePacketPlayerTitle() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Title.Serialize(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }
}