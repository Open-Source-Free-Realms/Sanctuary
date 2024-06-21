using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ProfileTypeEntry : ISerializableType
{
    public int Type { get; set; }
    public int ProfileId { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Type);
        writer.Write(ProfileId);
    }
}