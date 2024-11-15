using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public sealed class DbUser
{
    public ulong Guid { get; set; }

    public required string Username { get; set; }
    public required string Password { get; set; }

    public string? Session { get; set; }
    public DateTimeOffset? SessionCreated { get; set; }

    public int MaxCharacters { get; set; }

    public bool IsLocked { get; set; }
    public bool IsMember { get; set; }
    public bool IsAdmin { get; set; }

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? LastLogin { get; set; }

    public ICollection<DbCharacter> Characters { get; set; } = new HashSet<DbCharacter>();
}