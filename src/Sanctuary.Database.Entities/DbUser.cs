using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public sealed class DbUser
{
    public ulong Id { get; set; }

    public required string Username { get; set; }
    public required string Password { get; set; }

    public string? Session { get; set; }
    public DateTimeOffset? SessionCreated { get; set; }

    public int MaxCharacters { get; set; }

    public bool IsMember { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsMod { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? MutedUntil { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? LastLogin { get; set; }

    public ICollection<DbCharacter> Characters { get; set; } = [];
}