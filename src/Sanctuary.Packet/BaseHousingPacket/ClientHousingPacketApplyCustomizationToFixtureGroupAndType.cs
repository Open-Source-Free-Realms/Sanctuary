using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketApplyCustomizationToFixtureGroupAndType : BaseHousingPacket, IDeserializable<ClientHousingPacketApplyCustomizationToFixtureGroupAndType>
{
    public new const short OpCode = 59;

    public int ItemDefinitionId;
    public string FixtureGroup = string.Empty;
    public string FixtureType = string.Empty;

    public ClientHousingPacketApplyCustomizationToFixtureGroupAndType() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketApplyCustomizationToFixtureGroupAndType value)
    {
        value = new ClientHousingPacketApplyCustomizationToFixtureGroupAndType();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ItemDefinitionId))
            return false;

        if (!reader.TryRead(out string? fixtureGroup))
            return false;

        if (!reader.TryRead(out string? fixtureType))
            return false;

        value.FixtureGroup = fixtureGroup ?? string.Empty;
        value.FixtureType = fixtureType ?? string.Empty;

        return reader.RemainingLength == 0;
    }
}
