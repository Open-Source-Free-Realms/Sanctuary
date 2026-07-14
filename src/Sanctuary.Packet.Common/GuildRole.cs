using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildRole : ISerializableType
{
    public ulong Guid;

    /// <summary>
    /// Hardcoded ids
    /// 1 - Leader
    /// 2 - Officer
    /// 3 - Member
    /// 4 - Recruit
    /// </summary>
    public int Id;

    public int NameId;
    public int Rank;

    public static GuildRole Leader = new(1);
    public static GuildRole Officer = new(2);
    public static GuildRole Member = new(3);
    public static GuildRole Recruit = new(4);

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