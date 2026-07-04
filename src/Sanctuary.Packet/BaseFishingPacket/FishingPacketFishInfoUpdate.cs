using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketFishInfoUpdate : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public List<Packet.Common.ClientFishEntryInfo> ClientFishEntries = [];

    public FishingPacketFishInfoUpdate() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(ClientFishEntries);
        return writer.Buffer;
    }
}
