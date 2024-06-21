using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class AdventurersJournalInfoPacket : BaseAdventurersJournalPacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public Dictionary<int, AdventurersJournalRegionDefinition> Regions = new();
    public Dictionary<int, AdventurersJournalHubDefinition> Hubs = new();
    public Dictionary<int, AdventurersJournalHubQuestDefinition> HubQuests = new();
    public Dictionary<int, AdventurersJournalStickerDefinition> Stickers = new();

    public AdventurersJournalInfoPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Regions);
        writer.Write(Hubs);
        writer.Write(HubQuests);
        writer.Write(Stickers);

        return writer.Buffer;
    }
}