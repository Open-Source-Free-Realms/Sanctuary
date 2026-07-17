using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateMana : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public ulong Guid;

    public int Mana = 100;
    public int Unknown2 = 800;
    public int Unknown3 = 800;

    public PlayerUpdatePacketUpdateMana() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Mana);
        writer.Write(Unknown2);
        writer.Write(Unknown3);

        return writer.Buffer;
    }
}
