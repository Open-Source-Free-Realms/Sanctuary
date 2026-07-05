using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using Sanctuary.Gateway.Services.Models;

namespace Sanctuary.Gateway.Services;

public sealed class IpHistoryStore
{
    private static readonly string IpHistoryPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "IpHistory", "ip-history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Lock _sync = new();
    private Dictionary<ulong, IpHistoryEntry> _entryDictionary = new();
    private DateTime _lastLoadedWriteUtc = DateTime.MinValue;

    public List<IpHistoryEntry> Load()
    {
        lock (_sync)
        {
            EnsureCacheLoaded(forceReload: true);
            return SnapshotUnsafe();
        }
    }

    public void Save(List<IpHistoryEntry> entries)
    {
        lock (_sync)
        {
            EnsureDirectory();

            _entryDictionary = entries
                .Where(x => x is not null)
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => NormalizeEntry(g.Last()));

            SaveUnsafe();
        }
    }

    public void RecordLogin(ulong userId, string username, IEnumerable<string>? characterNames, string? ip)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();

            var normalizedUsername = Normalize(username);
            var normalizedCharacterNames = NormalizeDistinct(characterNames);

            if (!_entryDictionary.TryGetValue(userId, out var existing))
            {
                existing = _entryDictionary.Values.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(normalizedUsername) &&
                    Normalize(x.Username).Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    existing = new IpHistoryEntry
                    {
                        UserId = userId,
                        Username = normalizedUsername,
                        LastSeenUtc = DateTime.UtcNow,
                        CharacterNames = new List<string>(),
                        KnownIps = new List<string>()
                    };
                }
                else if (existing.UserId != 0)
                {
                    _entryDictionary.Remove(existing.UserId);
                }
            }

            existing.UserId = userId;
            existing.Username = normalizedUsername;
            existing.LastSeenUtc = DateTime.UtcNow;
            existing.CharacterNames = normalizedCharacterNames;

            if (!string.IsNullOrWhiteSpace(ip))
            {
                existing.KnownIps = NormalizeDistinct((existing.KnownIps ?? new List<string>()).Concat(new[] { ip! }));
            }
            else
            {
                existing.KnownIps = NormalizeDistinct(existing.KnownIps);
            }

            _entryDictionary[userId] = existing;
            SaveUnsafe();
        }
    }

    public List<string> GetKnownIpsForUser(ulong userId, string? username = null)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();

            var normalizedUsername = Normalize(username);
            var entry = FindByUserUnsafe(userId, normalizedUsername);

            return entry?.KnownIps?
                .Select(Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
    }

    public IpHistoryEntry? GetByUser(ulong userId, string? username = null)
    {
        lock (_sync)
        {
            EnsureCacheLoaded();
            return Clone(FindByUserUnsafe(userId, Normalize(username)));
        }
    }

    public void UpdateCharacterNameForUser(ulong userId, string oldName, string newName)
    {
        if (userId == 0 || string.IsNullOrWhiteSpace(newName))
            return;

        lock (_sync)
        {
            EnsureCacheLoaded();

            if (!_entryDictionary.TryGetValue(userId, out var entry))
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

            if (!_entryDictionary.TryGetValue(userId, out var entry))
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
        EnsureDirectory();

        var writeUtc = File.Exists(IpHistoryPath)
            ? File.GetLastWriteTimeUtc(IpHistoryPath)
            : DateTime.MinValue;

        if (!forceReload && _entryDictionary.Count > 0 && writeUtc == _lastLoadedWriteUtc)
            return;

        LoadUnsafe();
    }

    private void LoadUnsafe()
    {
        EnsureDirectory();

        if (!File.Exists(IpHistoryPath))
        {
            _entryDictionary = new Dictionary<ulong, IpHistoryEntry>();
            _lastLoadedWriteUtc = DateTime.MinValue;
            return;
        }

        var json = ReadAllTextWithRetry(IpHistoryPath);
        List<IpHistoryEntry> entries;

        if (string.IsNullOrWhiteSpace(json))
        {
            entries = new List<IpHistoryEntry>();
        }
        else
        {
            try
            {
                entries = JsonSerializer.Deserialize<List<IpHistoryEntry>>(json, JsonOptions) ?? new List<IpHistoryEntry>();
            }
            catch
            {
                entries = new List<IpHistoryEntry>();
            }
        }

        _entryDictionary = entries
            .Where(x => x is not null)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => NormalizeEntry(g.Last()));

        _lastLoadedWriteUtc = File.Exists(IpHistoryPath)
            ? File.GetLastWriteTimeUtc(IpHistoryPath)
            : DateTime.MinValue;
    }

    private void SaveUnsafe()
    {
        EnsureDirectory();

        var entries = _entryDictionary.Values
            .Select(Clone)
            .OrderBy(x => x.UserId)
            .ToList();

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        WriteAllTextWithRetry(IpHistoryPath, json);
        _lastLoadedWriteUtc = File.GetLastWriteTimeUtc(IpHistoryPath);
    }

    private List<IpHistoryEntry> SnapshotUnsafe()
    {
        return _entryDictionary.Values
            .Select(Clone)
            .OrderBy(x => x.UserId)
            .ToList();
    }

    private IpHistoryEntry? FindByUserUnsafe(ulong userId, string? normalizedUsername)
    {
        if (userId != 0 && _entryDictionary.TryGetValue(userId, out var byUserId))
            return byUserId;

        if (!string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return _entryDictionary.Values.FirstOrDefault(x =>
                Normalize(x.Username).Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static IpHistoryEntry NormalizeEntry(IpHistoryEntry entry)
    {
        return new IpHistoryEntry
        {
            UserId = entry.UserId,
            Username = Normalize(entry.Username),
            CharacterNames = NormalizeDistinct(entry.CharacterNames),
            KnownIps = NormalizeDistinct(entry.KnownIps),
            LastSeenUtc = entry.LastSeenUtc
        };
    }

    private static IpHistoryEntry? Clone(IpHistoryEntry? entry)
    {
        if (entry is null)
            return null;

        return new IpHistoryEntry
        {
            UserId = entry.UserId,
            Username = entry.Username,
            CharacterNames = new List<string>(entry.CharacterNames ?? new List<string>()),
            KnownIps = new List<string>(entry.KnownIps ?? new List<string>()),
            LastSeenUtc = entry.LastSeenUtc
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

    private static void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(IpHistoryPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}
