using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.Authoring;

public interface IRedeemCodeManager
{
    IReadOnlyList<DbRedeemCode> GetAll();

    bool TryGet(string code, out DbRedeemCode redeemCode);

    /// <summary>
    /// Creates a code; null or "random" generates one.
    /// </summary>
    DbRedeemCode? Create(string? code, ulong? createdBy, out string? error);

    /// <summary>
    /// Applies an edit and saves. Returns the error, or null.
    /// </summary>
    string? Update(string code, Func<DbRedeemCode, string?> edit);

    bool Delete(string code);

    /// <summary>
    /// Redeems a code for a player. Returns the message to show them.
    /// </summary>
    string Redeem(Player player, string code);
}

/// <summary>
/// Codes players redeem with !redeem for items (custom ones included) and coins. The client's own
/// "Enter a game code" box calls SOE's commerce web service over HTTPS, which can't be pointed here.
/// </summary>
public sealed partial class RedeemCodeManager : IRedeemCodeManager
{
    public const int MaxItems = 8;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(1);

    private const string RandomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly ILogger _logger;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _redeemLock = new();
    private readonly ConcurrentDictionary<ulong, (int Count, DateTimeOffset WindowStart)> _failedAttempts = new();

    public RedeemCodeManager(ILogger<RedeemCodeManager> logger, IRewardManager rewardManager, IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    [GeneratedRegex("^[A-Z0-9-]{4,32}$")]
    private static partial Regex CodeRegex();

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public IReadOnlyList<DbRedeemCode> GetAll()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.RedeemCodes.AsNoTracking().OrderBy(x => x.Code).ToList();
    }

    public bool TryGet(string code, out DbRedeemCode redeemCode)
    {
        var normalized = Normalize(code);

        using var dbContext = _dbContextFactory.CreateDbContext();

        redeemCode = dbContext.RedeemCodes.AsNoTracking().SingleOrDefault(x => x.Code == normalized)!;

        return redeemCode is not null;
    }

    public DbRedeemCode? Create(string? code, ulong? createdBy, out string? error)
    {
        error = null;

        var normalized = code is null || code.Equals("random", StringComparison.OrdinalIgnoreCase) ? GenerateCode() : Normalize(code);

        if (!CodeRegex().IsMatch(normalized))
        {
            error = "Codes are 4-32 letters, digits or '-'.";
            return null;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (dbContext.RedeemCodes.Any(x => x.Code == normalized))
        {
            error = $"Code {normalized} already exists.";
            return null;
        }

        var redeemCode = new DbRedeemCode { Code = normalized, CreatedBy = createdBy };

        dbContext.RedeemCodes.Add(redeemCode);
        dbContext.SaveChanges();

        return redeemCode;
    }

    public string? Update(string code, Func<DbRedeemCode, string?> edit)
    {
        var normalized = Normalize(code);

        lock (_redeemLock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var redeemCode = dbContext.RedeemCodes.SingleOrDefault(x => x.Code == normalized);

            if (redeemCode is null)
                return $"No code {normalized}.";

            if (edit(redeemCode) is { } error)
                return error;

            dbContext.SaveChanges();

            return null;
        }
    }

    public bool Delete(string code)
    {
        var normalized = Normalize(code);

        lock (_redeemLock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.RedeemCodeUses.Where(x => x.Code == normalized).ExecuteDelete();

            return dbContext.RedeemCodes.Where(x => x.Code == normalized).ExecuteDelete() > 0;
        }
    }

    public string Redeem(Player player, string code)
    {
        var now = DateTimeOffset.UtcNow;

        if (_failedAttempts.TryGetValue(player.Guid, out var attempts)
            && now - attempts.WindowStart < FailedAttemptWindow && attempts.Count >= MaxFailedAttempts)
            return "Too many attempts; wait a minute and try again.";

        var normalized = Normalize(code);
        List<CodeItem> items;
        int coins;

        lock (_redeemLock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var redeemCode = dbContext.RedeemCodes.SingleOrDefault(x => x.Code == normalized);

            // One message for unknown and disabled, so codes can't be probed.
            if (redeemCode is null || !redeemCode.Enabled)
                return Fail(player, now, "That code isn't valid.");

            if (redeemCode.StartsAt is { } startsAt && now < startsAt)
                return Fail(player, now, "That code isn't active yet.");

            if (redeemCode.ExpiresAt is { } expiresAt && now >= expiresAt)
                return Fail(player, now, "That code has expired.");

            if (redeemCode.MembersOnly && player.MembershipStatus == 0)
                return "That code is for members only.";

            if (redeemCode.MaxUses > 0 && redeemCode.Uses >= redeemCode.MaxUses)
                return "That code has been fully redeemed.";

            var use = dbContext.RedeemCodeUses.SingleOrDefault(x => x.Code == normalized && x.CharacterId == player.CharacterId);

            if (redeemCode.PerCharacterLimit > 0 && use is not null && use.Count >= redeemCode.PerCharacterLimit)
                return "You've already redeemed that code.";

            items = CodeItem.Read(redeemCode.Items);
            coins = redeemCode.Coins;

            if (items.Count == 0 && coins == 0)
                return "That code has no rewards yet.";

            if (use is null)
            {
                use = new DbRedeemCodeUse { Code = normalized, CharacterId = player.CharacterId };
                dbContext.RedeemCodeUses.Add(use);
            }

            use.Count++;
            use.LastUsed = now;
            redeemCode.Uses++;

            // Counted before granting, so a crash can't hand out a code twice.
            dbContext.SaveChanges();
        }

        _failedAttempts.TryRemove(player.Guid, out _);

        foreach (var item in items)
        {
            if (!_rewardManager.TryGrantItem(player, item.ItemId, 0, item.Quantity))
                _logger.LogError("Code {Code}: failed to grant item {Item} x{Quantity} to {Player}.", normalized, item.ItemId, item.Quantity, player.Name.FullName);
        }

        if (coins > 0 && !_rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins))
            _logger.LogError("Code {Code}: failed to grant {Coins} coins to {Player}.", normalized, coins, player.Name.FullName);

        _logger.LogInformation("{Player} redeemed code {Code}.", player.Name.FullName, normalized);

        var parts = items.Select(x => $"{ItemName(x.ItemId)} x{x.Quantity}").ToList();

        if (coins > 0)
            parts.Add($"{coins} coins");

        return $"Code redeemed! You received {string.Join(", ", parts)}.";
    }

    private string Fail(Player player, DateTimeOffset now, string message)
    {
        _failedAttempts.AddOrUpdate(player.Guid,
            _ => (1, now),
            (_, previous) => now - previous.WindowStart >= FailedAttemptWindow ? (1, now) : (previous.Count + 1, previous.WindowStart));

        return message;
    }

    private string ItemName(int itemId) =>
        _resourceManager.ClientItemDefinitions.TryGetValue(itemId, out var item) && !string.IsNullOrEmpty(item.Comment) ? item.Comment : $"item {itemId}";

    private static string GenerateCode()
    {
        // FR-XXXX-XXXX from an alphabet without look-alike characters.
        Span<char> chars = stackalloc char[8];

        for (var i = 0; i < chars.Length; i++)
            chars[i] = RandomAlphabet[RandomNumberGenerator.GetInt32(RandomAlphabet.Length)];

        return $"FR-{new string(chars[..4])}-{new string(chars[4..])}";
    }
}

/// <summary>
/// One item reward of a code, stored as JSON in DbRedeemCode.Items.
/// </summary>
public sealed class CodeItem
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;

    public static List<CodeItem> Read(string? json) => CustomQuestJson.ReadList<CodeItem>(json);

    public static string Write(List<CodeItem> items) => CustomQuestJson.Write(items);
}
