using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketAbilityDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public int AbilityId;
    public int AbilityType;   // field 2 - type/category
    public int NameId;        // field 3
    public int IconId;        // field 4
    public int CooldownMs;    // field 5 - recharge time
    public int CastTimeMs;    // field 6

    public AbilityPacketAbilityDefinition() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AbilityId);
        writer.Write(AbilityType);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(CooldownMs);
        writer.Write(CastTimeMs);

        return writer.Buffer;
    }
}
