using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientQuestData : ISerializableType
{
    public List<ClientQuest> Quests = [];

    public int CompletedCount;
    public int ActiveQuestId;
    public bool Unknown = true;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Quests);
        writer.Write(CompletedCount);
        writer.Write(ActiveQuestId);
        writer.Write(Unknown);
    }
}
