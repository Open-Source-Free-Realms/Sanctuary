using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketSetDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public int AbilityId;
    public int CooldownRemainingMs;
    public int TotalCooldownMs;

    public AbilityPacketSetDefinition() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AbilityId);
        writer.Write(CooldownRemainingMs);
        writer.Write(TotalCooldownMs);

        return writer.Buffer;
    }
}
