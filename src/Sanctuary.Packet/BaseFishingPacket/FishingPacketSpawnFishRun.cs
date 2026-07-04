using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// Sub-opcode 18: bool Flag + int Unknown2 + string Name + UnderwaterFishSpawnInfo[]
public class FishingPacketSpawnFishRun : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 18;

    public bool Unknown;
    public int Unknown2;
    public string? Unknown3;
    public List<UnderwaterFishSpawnInfo> UnderwaterFishSpawns = [];

    public FishingPacketSpawnFishRun() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3 ?? "");
        writer.Write(UnderwaterFishSpawns);
        return writer.Buffer;
    }
}
