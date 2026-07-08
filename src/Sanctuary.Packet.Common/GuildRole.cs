using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildRole : ISerializableType
{
    public ulong Guid;

    public int Id;

    public int NameId;
    public int Rank;

    public static GuildRole Leader = new(1);  // 1 leader
    public static GuildRole Officer = new(2); // 2 officer
    public static GuildRole Member = new(3);  // 3 member
    public static GuildRole Recruit = new(4); // 4 recruit

    public GuildRole(int id)
    {
        Id = id;
        Rank = id;
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);

        writer.Write(Id);
        writer.Write(NameId);

        writer.Write(Rank);
    }
}
