using System;
using System.Numerics;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbCharacter
{
    public ulong Guid { get; set; }

    public Guid? Ticket { get; set; }

    public required string FirstName { get; set; }
    public string? LastName { get; set; }

    public int Model { get; set; }
    public required string Head { get; set; }
    public required string Hair { get; set; }
    public string? ModelCustomization { get; set; }
    public string? FacePaint { get; set; }
    public required string SkinTone { get; set; }
    public int EyeColor { get; set; }
    public int HairColor { get; set; }

    public Vector4 Position { get; set; }
    public Quaternion Rotation { get; set; }

    public int ActiveProfileId { get; set; }

    public int Gender { get; set; }

    public int? ActiveTitleId { get; set; }

    public float VipRank { get; set; }
    public int MembershipStatus { get; set; }

    public int ChatBubbleForegroundColor { get; set; } = 0x063C67;
    public int ChatBubbleBackgroundColor { get; set; } = 0xD4E2F0;
    public int ChatBubbleSize { get; set; } = 1;

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLogin { get; set; }

    public ICollection<DbItem> Items { get; set; } = new HashSet<DbItem>();
    public ICollection<DbTitle> Titles { get; set; } = new HashSet<DbTitle>();
    public ICollection<DbMount> Mounts { get; set; } = new HashSet<DbMount>();
    public ICollection<DbProfile> Profiles { get; set; } = new HashSet<DbProfile>();

    public ulong UserGuid { get; set; }
    public DbUser User { get; set; } = null!;
}