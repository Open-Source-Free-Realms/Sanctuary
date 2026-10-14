using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketDetonateProjectile : BaseAbilityPacket, IDeserializable<AbilityPacketDetonateProjectile>
{
    public new const short OpCode = 14;

    public ulong Guid;
    public int CompositeEffectId;
    public int Unknown;
    public int Unknown2;

    public AbilityPacketDetonateProjectile() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out AbilityPacketDetonateProjectile value)
    {
        value = new AbilityPacketDetonateProjectile();
        var reader = new PacketReader(data);
        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.CompositeEffectId))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
