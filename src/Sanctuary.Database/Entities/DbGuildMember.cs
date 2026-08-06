using System;

namespace Sanctuary.Database.Entities;

public class DbGuildMember
{
    /// <summary>
    /// Same as <see cref="DbCharacter.Id"/>, but independent PK.
    /// </summary>
    public ulong Id { get; set; }

    public int Role { get; set; }

    public DateTimeOffset Joined { get; set; } = DateTimeOffset.UtcNow;

    public ulong GuildId { get; set; }
    public DbGuild Guild { get; set; } = null!;

    public DbCharacter Character { get; set; } = null!;
}