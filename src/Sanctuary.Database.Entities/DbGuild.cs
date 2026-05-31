using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbGuild
{
    public ulong Id { get; set; }

    public required string Name { get; set; }

    public int MaxMembers { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DbGuildMember> Members { get; set; } = new HashSet<DbGuildMember>();
}