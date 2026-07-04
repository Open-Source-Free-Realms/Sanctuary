using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class FishingPacketRegisterPlayerResponse : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public FishingPlayerConfig FishingPlayerConfig = new();
    public FishingZoneConfig FishingZoneConfig = new();

    public List<int> FishModelIds = [];

    public List<ClientFishEntryInfo> ClientFishEntries = [];

    public FishingPacketRegisterPlayerResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        FishingPlayerConfig.Serialize(writer);
        FishingZoneConfig.Serialize(writer);

        writer.Write(FishModelIds);

        writer.Write(ClientFishEntries);

        return writer.Buffer;
    }
}