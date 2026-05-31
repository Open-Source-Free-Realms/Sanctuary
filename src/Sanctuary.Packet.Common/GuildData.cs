using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildData : ISerializableType
{
    public ulong Guid;

    public string? Name;
    public string? Unknown;

    public bool Unknown2;
    public bool CanRenameGuild;

    public string? Motd;

    // Server side
    public int MaxMembers;

    public Dictionary<ulong, GuildMember> Members = [];

    public Dictionary<int, GuildRole> Roles = DefaultRoles;

    public static Dictionary<int, GuildRole> DefaultRoles = new()
    {
        { GuildRole.Leader.Id, GuildRole.Leader },
        { GuildRole.Officer.Id, GuildRole.Officer },
        { GuildRole.Member.Id, GuildRole.Member },
        { GuildRole.Recruit.Id, GuildRole.Recruit }
    };

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);

        writer.Write(Name);
        writer.Write(Unknown);

        writer.Write(Unknown2);
        writer.Write(CanRenameGuild);

        writer.Write(Members.Count);

        foreach (var member in Members)
        {
            writer.Write(member.Key.GetHashCode());

            member.Value.Serialize(writer);
        }

        writer.Write(Roles);
    }
}