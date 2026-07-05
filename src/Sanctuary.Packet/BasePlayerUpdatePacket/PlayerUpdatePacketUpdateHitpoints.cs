using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateHitpoints : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public ulong Guid;
    public int CurrentHitpoints;
    public int MaxHitpoints;
    public int Unknown;

    public PlayerUpdatePacketUpdateHitpoints() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(CurrentHitpoints);
        writer.Write(MaxHitpoints);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
