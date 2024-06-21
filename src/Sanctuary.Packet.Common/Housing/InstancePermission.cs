using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InstancePermission : ISerializableType
{
    public ulong Unknown;
    public int Unknown2;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);
        writer.Write(Unknown2);
    }
}