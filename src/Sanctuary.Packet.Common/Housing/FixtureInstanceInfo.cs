using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FixtureInstanceInfo : ISerializableType
{
    public CouplingDisplayData CouplingDisplay = new();

    public ulong FixtureGuid;
    public int ItemDefinitionId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(FixtureGuid);
        writer.Write(ItemDefinitionId);

        CouplingDisplay.Serialize(writer);
    }
}