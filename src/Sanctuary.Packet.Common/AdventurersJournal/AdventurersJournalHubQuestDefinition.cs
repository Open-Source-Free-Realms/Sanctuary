using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AdventurersJournalHubQuestDefinition : ISerializableType
{
    public int HubId;

    public int Id;

    public int Unknown;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HubId);

        writer.Write(Id);

        writer.Write(Unknown);
    }
}