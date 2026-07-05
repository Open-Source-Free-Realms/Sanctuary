using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using Sanctuary.Gateway.Services.Models;

namespace Sanctuary.Gateway.Services;

public sealed class BanStore
{
    private static readonly string BanListPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "Bans", "banlist.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Lock _sync = new();
    private Dictionary<ulong, BanEntry> _banDictionary = new();
    private DateTime _lastLoadedWriteUtc = DateTime.MinValue;

    public List<BanEntry> Load()
    {
        lock (_sync)
        {
            EnsureCacheLoaded(forceReload: true);
            return SnapshotUnsafe();
        }
    }

    public void Save(List<BanEntry> bans)
    {
        lock (_sync)
        {
            EnsureDirectoryAndFile();

            _banDictionary = bans
                .Where(x => x is not null)
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => NormalizeEntry(g.Last()));

            SaveUnsafe();
        }
    }

    public bool ReloadIfChanged()
    {
        lock (_sync)
        {
            EnsureDirectoryAndFile();

            var writeUtc = File.GetLastWriteTimeUtc(BanListPath);
            if (writeUtc == _lastLoadedWriteUtc)
                return false;

            LoadUnsafe();
            return true;
        }
    }

    public BanEntry? FindMatch(ulong userId, string? username, IEnumerable<string>? characterNames, string? ip)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();

            if (userId != 0 && _banDictionary.TryGetValue(userId, out var directMatch))
                return Clone(directMatch);

            var normalizedUsername = Normalize(username);
            var normalizedIp = Normalize(ip);

            var normalizedCharacterNames = (characterNames ?? Enumerable.Empty<string>())
                .Select(Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var ban in _banDictionary.Values)
            {
                if (!string.IsNullOrWhiteSpace(normalizedUsername) &&
                    Normalize(ban.Username).Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return Clone(ban);
                }

                if (!string.IsNullOrWhiteSpace(normalizedIp) &&
                    (ban.KnownIps ?? new List<string>())
                        .Any(x => Normalize(x).Equals(normalizedIp, StringComparison.OrdinalIgnoreCase)))
                {
                    return Clone(ban);
                }

                if (normalizedCharacterNames.Count > 0 &&
                    (ban.CharacterNames ?? new List<string>())
                        .Any(x => normalizedCharacterNames.Contains(Normalize(x))))
                {
                    return Clone(ban);
                }
            }

            return null;
        }
    }

    public bool IsBanned(ulong userId, string? username, IEnumerable<string>? characterNames, string? ip)
    {
        return FindMatch(userId, username, characterNames, ip) is not null;
    }

    public bool IsUserIdBanned(ulong userId)
    {
        if (userId == 0)
            return false;

        lock (_sync)
        {
            EnsureCacheLoaded();
            return _banDictionary.ContainsKey(userId);
        }
    }

    public void AddOrUpdateBan(BanEntry entry)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();

            var normalizedEntry = NormalizeEntry(entry);

            if (normalizedEntry.UserId != 0 && _banDictionary.TryGetValue(normalizedEntry.UserId, out var existingByUserId))
            {
                MergeInto(existingByUserId, normalizedEntry);
                SaveUnsafe();
                return;
            }

            var existing = _banDictionary.Values.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Username) &&
                !string.IsNullOrWhiteSpace(normalizedEntry.Username) &&
                Normalize(x.Username).Equals(normalizedEntry.Username, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                _banDictionary[normalizedEntry.UserId] = normalizedEntry;
            }
            else
            {
                if (existing.UserId != 0)
                    _banDictionary.Remove(existing.UserId);

                MergeInto(existing, normalizedEntry);
                _banDictionary[existing.UserId] = existing;
            }

            SaveUnsafe();
        }
    }

    public bool RemoveBanByUserId(ulong userId)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();

            var removed = _banDictionary.Remove(userId);
            if (removed)
                SaveUnsafe();

            return removed;
        }
    }

    public bool RemoveBanByUsername(string username)
    {
        var normalizedUsername = Normalize(username);

        lock (_sync)
        {
            EnsureCacheLoaded();

            var existing = _banDictionary.Values.FirstOrDefault(x =>
                Normalize(x.Username).Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
                return false;

            var removed = _banDictionary.Remove(existing.UserId);
            if (removed)
                SaveUnsafe();

            return removed;
        }
    }

    public void UpdateCharacterNameForUser(ulong userId, string oldName, string newName)
    {
        if (userId == 0 || string.IsNullOrWhiteSpace(newName))
            return;

        lock (_sync)
        {
            EnsureCacheLoaded();

            if (!_banDictionary.TryGetValue(userId, out var entry))
                return;

            var names = entry.CharacterNames ?? new List<string>();
            var normalizedOldName = Normalize(oldName);
            var normalizedNewName = Normalize(newName);

            if (!string.IsNullOrWhiteSpace(normalizedOldName))
            {
                names = names
                    .Where(x => !Normalize(x).Equals(normalizedOldName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            names.Add(normalizedNewName);
            entry.CharacterNames = NormalizeDistinct(names);
            SaveUnsafe();
        }
    }

    public void RemoveCharacterNameForUser(ulong userId, string deletedName)
    {
        if (userId == 0 || string.IsNullOrWhiteSpace(deletedName))
            return;

        lock (_sync)
        {
            EnsureCacheLoaded();

            if (!_banDictionary.TryGetValue(userId, out var entry))
                return;

            var normalizedDeletedName = Normalize(deletedName);
            entry.CharacterNames = (entry.CharacterNames ?? new List<string>())
                .Where(x => !Normalize(x).Equals(normalizedDeletedName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SaveUnsafe();
        }
    }

    private void EnsureCacheLoaded(bool forceReload = false)
    {
        EnsureDirectoryAndFile();

        var writeUtc = File.GetLastWriteTimeUtc(BanListPath);
        if (!forceReload && _banDictionary.Count > 0 && writeUtc == _lastLoadedWriteUtc)
            return;

        LoadUnsafe();
    }

    private void LoadUnsafe()
    {
        EnsureDirectoryAndFile();

        var json = ReadAllTextWithRetry(BanListPath);
        List<BanEntry> bans;

        if (string.IsNullOrWhiteSpace(json))
        {
            bans = new List<BanEntry>();
        }
        else
        {
            try
            {
                bans = JsonSerializer.Deserialize<List<BanEntry>>(json, JsonOptions) ?? new List<BanEntry>();
            }
            catch
            {
                bans = new List<BanEntry>();
            }
        }

        _banDictionary = bans
            .Where(x => x is not null)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => NormalizeEntry(g.Last()));

        _lastLoadedWriteUtc = File.GetLastWriteTimeUtc(BanListPath);
    }

    private void SaveUnsafe()
    {
        EnsureDirectoryAndFile();

        var bans = _banDictionary.Values
            .Select(Clone)
            .OrderBy(x => x.UserId)
            .ToList();

        var json = JsonSerializer.Serialize(bans, JsonOptions);
        WriteAllTextWithRetry(BanListPath, json);
        _lastLoadedWriteUtc = File.GetLastWriteTimeUtc(BanListPath);
    }

    private List<BanEntry> SnapshotUnsafe()
    {
        return _banDictionary.Values
            .Select(Clone)
            .OrderBy(x => x.UserId)
            .ToList();
    }

    private static void MergeInto(BanEntry destination, BanEntry source)
    {
        destination.UserId = source.UserId != 0 ? source.UserId : destination.UserId;
        destination.Username = string.IsNullOrWhiteSpace(source.Username) ? destination.Username : source.Username;
        destination.Reason = source.Reason;
        destination.BannedBy = source.BannedBy;
        destination.BannedAtUtc = source.BannedAtUtc;
        destination.CharacterNames = NormalizeDistinct((destination.CharacterNames ?? new List<string>()).Concat(source.CharacterNames ?? new List<string>()));
        destination.KnownIps = NormalizeDistinct((destination.KnownIps ?? new List<string>()).Concat(source.KnownIps ?? new List<string>()));
    }

    private static BanEntry NormalizeEntry(BanEntry entry)
    {
        return new BanEntry
        {
            UserId = entry.UserId,
            Username = Normalize(entry.Username),
            CharacterNames = NormalizeDistinct(entry.CharacterNames),
            KnownIps = NormalizeDistinct(entry.KnownIps),
            Reason = entry.Reason?.Trim() ?? string.Empty,
            BannedBy = entry.BannedBy?.Trim() ?? string.Empty,
            BannedAtUtc = entry.BannedAtUtc
        };
    }

    private static BanEntry Clone(BanEntry entry)
    {
        return new BanEntry
        {
            UserId = entry.UserId,
            Username = entry.Username,
            CharacterNames = new List<string>(entry.CharacterNames ?? new List<string>()),
            KnownIps = new List<string>(entry.KnownIps ?? new List<string>()),
            Reason = entry.Reason,
            BannedBy = entry.BannedBy,
            BannedAtUtc = entry.BannedAtUtc
        };
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static List<string> NormalizeDistinct(IEnumerable<string>? values)
    {
        return (values ?? Enumerable.Empty<string>())
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ReadAllTextWithRetry(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private static void WriteAllTextWithRetry(string path, string content)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.WriteAllText(path, content);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private static void EnsureDirectoryAndFile()
    {
        var directory = Path.GetDirectoryName(BanListPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(BanListPath))
            File.WriteAllText(BanListPath, "[]");
    }
}
