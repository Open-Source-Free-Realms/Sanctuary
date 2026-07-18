using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateMana : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public ulong Guid;

    public int BossMana = 100;
    public int MaxMana = 800;
    public int Mana = 800;

    public PlayerUpdatePacketUpdateMana() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(BossMana);
        writer.Write(MaxMana);
        writer.Write(Mana);

        return writer.Buffer;
    }
}
