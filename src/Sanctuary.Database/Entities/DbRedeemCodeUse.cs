using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// How many times a character has redeemed a code.
/// </summary>
public class DbRedeemCodeUse
{
    public string Code { get; set; } = null!;

    public ulong CharacterId { get; set; }

    public int Count { get; set; }

    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;
}
