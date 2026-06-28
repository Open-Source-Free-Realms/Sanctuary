using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent server→client when an attack has been fully processed (OpCode 32, SubOpCode 7).
/// </summary>
public class CombatPacketAttackProcessed : BaseCombatPacket, ISerializablePacket
{
    public new const short OpCode = 7;

    public CombatPacketAttackProcessed() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
