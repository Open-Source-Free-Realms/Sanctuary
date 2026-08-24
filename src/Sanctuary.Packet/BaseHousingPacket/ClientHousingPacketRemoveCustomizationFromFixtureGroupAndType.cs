using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType : BaseHousingPacket, IDeserializable<ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType>
{
    public new const short OpCode = 60;

    public string FixtureGroup = string.Empty;
    public string FixtureType = string.Empty;

    public ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType value)
    {
        value = new ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
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
