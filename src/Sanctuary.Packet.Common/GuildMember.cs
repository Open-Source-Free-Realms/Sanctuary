using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildMember : ISerializableType
{
    public ulong Guid;

    public int Role;

    public NameData Name = new();

    public bool Online;

    public int WorldId;

    public int Unknown;

    public int ProfileId;
    public int ProfileRank;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);

        writer.Write(Role);

        Name.Serialize(writer);

        writer.Write(Online);

        writer.Write(WorldId);

        writer.Write(Unknown);

        writer.Write(ProfileId);
        writer.Write(ProfileRank);
    }
}