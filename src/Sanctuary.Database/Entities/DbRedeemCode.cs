using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A code players redeem with !redeem for items and coins.
/// </summary>
public class DbRedeemCode
{
    /// <summary>
    /// Stored upper-case; redeeming is case-insensitive.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// JSON list of item rewards.
    /// </summary>
    public string Items { get; set; } = "[]";

    public int Coins { get; set; }

    /// <summary>
    /// Total redemptions allowed across everyone; 0 is unlimited.
    /// </summary>
    public int MaxUses { get; set; }

    public int Uses { get; set; }

    /// <summary>
    /// Redemptions allowed per character; 0 is unlimited.
    /// </summary>
    public int PerCharacterLimit { get; set; } = 1;

    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool MembersOnly { get; set; }

    public bool Enabled { get; set; } = true;

    public string? Note { get; set; }

    public ulong? CreatedBy { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
