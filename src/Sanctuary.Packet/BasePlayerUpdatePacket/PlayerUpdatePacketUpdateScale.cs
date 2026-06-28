using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Updates an entity's scale (OpCode 35, SubOpCode 13).
/// </summary>
public class PlayerUpdatePacketUpdateScale : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public ulong Guid;
    public float Scale;

    public PlayerUpdatePacketUpdateScale() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Scale);

        return writer.Buffer;
    }
}
