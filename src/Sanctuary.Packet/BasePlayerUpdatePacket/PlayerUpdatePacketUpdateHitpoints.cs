using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Broadcasts an entity's current/max HP to visible players (OpCode 35, SubOpCode 5).
/// </summary>
public class PlayerUpdatePacketUpdateHitpoints : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public ulong Guid;
    public int CurrentHitpoints;
    public int MaxHitpoints;

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

        return writer.Buffer;
    }
}
