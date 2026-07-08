using System;

namespace Sanctuary.Database.Entities;

public class DbGuildMember
{
    // Same value as DbCharacter.Id.
    public ulong Id { get; set; }

    public int Role { get; set; }

    public DateTimeOffset Joined { get; set; } = DateTimeOffset.UtcNow;

    public ulong GuildId { get; set; }
    public DbGuild Guild { get; set; } = null!;

    public DbCharacter Character { get; set; } = null!;
}
