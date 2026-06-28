using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Broadcasts a hitpoint modification event (damage/heal) to visible players (OpCode 35, SubOpCode 35).
/// Shows a floating combat number on the target.
/// </summary>
public class PlayerUpdatePacketHitPointModification : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 35;

    public ulong TargetGuid;

    /// <summary>
    /// The HP change amount. Negative = damage, Positive = heal.
    /// </summary>
    public int Amount;

    /// <summary>
    /// The source entity GUID that caused this modification.
    /// </summary>
    public ulong SourceGuid;

    public PlayerUpdatePacketHitPointModification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TargetGuid);
        writer.Write(Amount);
        writer.Write(SourceGuid);

        return writer.Buffer;
    }
}
