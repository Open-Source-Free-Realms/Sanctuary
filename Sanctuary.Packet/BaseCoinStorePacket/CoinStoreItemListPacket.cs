using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CoinStoreItemListPacket : BaseCoinStorePacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public Dictionary<int, ItemDefinitionMetaData> StaticItems = new();

    public Dictionary<int, ItemDefinitionMetaData> DynamicItems = new();

    public CoinStoreItemListPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(StaticItems);
        writer.Write(DynamicItems);

        return writer.Buffer;
    }
}