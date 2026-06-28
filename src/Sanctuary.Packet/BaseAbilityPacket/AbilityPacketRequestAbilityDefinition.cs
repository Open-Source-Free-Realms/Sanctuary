using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketRequestAbilityDefinition : BaseAbilityPacket, IDeserializable<AbilityPacketRequestAbilityDefinition>
{
    public new const short OpCode = 12;

    public int AbilityId;

    public AbilityPacketRequestAbilityDefinition() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out AbilityPacketRequestAbilityDefinition value)
    {
        value = new AbilityPacketRequestAbilityDefinition();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.AbilityId))
            return false;

        return true;
    }
}
