using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class EntityDetails : ISerializableType
{
    public ulong EntityKey;
    public int LastServerId;
    public int Status;

    public byte[] ApplicationData = Array.Empty<byte>();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(EntityKey);
        writer.Write(LastServerId);
        writer.Write(Status);

        writer.WritePayload(ApplicationData);
    }
}