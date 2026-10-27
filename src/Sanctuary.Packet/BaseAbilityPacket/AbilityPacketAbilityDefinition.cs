using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class AbilityPacketAbilityDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public ClientAbilityDefinition? Definition;

    public int AbilityId;
    public int NameId;
    public int DescriptionId;
    public int IconId;
    public float CastSeconds;
    public int ManaCost;
    public int ManaCostPerSecond;
    public int AuraDuration;
    public int MaxAoeTargets;

    public AbilityPacketAbilityDefinition() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        if (Definition is not null)
        {
            Definition.Serialize(writer);
            return writer.Buffer;
        }

        writer.Write(AbilityId);
        writer.Write(false);
        writer.Write(false);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(IconId);
        writer.Write(CastSeconds);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(ManaCost);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(0);
        writer.Write(ManaCostPerSecond);
        writer.Write(false);
        writer.Write(AuraDuration);
        writer.Write(0);
        writer.Write(MaxAoeTargets);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(false);
        writer.Write(0);
        writer.Write(0);
        writer.Write(false);
        writer.Write(false);
        writer.Write(false);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(false);

        return writer.Buffer;
    }
}
