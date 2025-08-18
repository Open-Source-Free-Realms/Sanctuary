using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ReferenceDataPacketItemCategoryDefinitions : ReferenceDataPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public Dictionary<int, ItemCategoryDefinition> ItemCategories = new();
    public Dictionary<int, List<ItemCategoryGroupDefinition>> ItemCategoryGroups = new();

    public ReferenceDataPacketItemCategoryDefinitions() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ItemCategories);

        writer.Write(ItemCategoryGroups.Sum(x => x.Value.Count));

        foreach (var itemCategoryGroup in ItemCategoryGroups)
        {
            foreach (var itemCategoryGroupValue in itemCategoryGroup.Value)
            {
                writer.Write(itemCategoryGroup.Key);
                itemCategoryGroupValue.Serialize(writer);
            }
        }

        return writer.Buffer;
    }
}