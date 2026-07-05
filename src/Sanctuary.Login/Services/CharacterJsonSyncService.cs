using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Sanctuary.Login.Services;

internal sealed class CharacterJsonSyncService
{
    private static readonly string BanListPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "Bans", "banlist.json");

    private static readonly string IpHistoryPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "IpHistory", "ip-history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _syncRoot = new();

    public void RemoveCharacterNameForUser(ulong userId, string? characterName)
    {
        if (userId == 0 || string.IsNullOrWhiteSpace(characterName))
            return;

        lock (_syncRoot)
        {
            UpdateBanList(userId, characterName!);
            UpdateIpHistory(userId, characterName!);
        }
    }

    private static void UpdateBanList(ulong userId, string characterName)
    {
        EnsureFile(BanListPath);
        var bans = ReadWithRetry<List<BanEntryDto>>(BanListPath) ?? new List<BanEntryDto>();
        var ban = bans.FirstOrDefault(x => x.UserId == userId);

        if (ban is null)
            return;

        ban.CharacterNames = NormalizeDistinct(ban.CharacterNames)
            .Where(x => !x.Equals(characterName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        WriteWithRetry(BanListPath, bans);
    }

    private static void UpdateIpHistory(ulong userId, string characterName)
    {
        EnsureFile(IpHistoryPath);
        var entries = ReadWithRetry<List<IpHistoryEntryDto>>(IpHistoryPath) ?? new List<IpHistoryEntryDto>();
        var entry = entries.FirstOrDefault(x => x.UserId == userId);

        if (entry is null)
            return;

        entry.CharacterNames = NormalizeDistinct(entry.CharacterNames)
            .Where(x => !x.Equals(characterName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        WriteWithRetry(IpHistoryPath, entries);
    }

    private static T? ReadWithRetry<T>(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length == 0)
                    return default;

                return JsonSerializer.Deserialize<T>(stream, JsonOptions);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(25);
            }
            catch (JsonException) when (attempt < 4)
            {
                Thread.Sleep(25);
            }
        }

        return default;
    }

    private static void WriteWithRetry<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var tempPath = path + ".tmp";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                    File.Copy(tempPath, path, overwrite: true);
                else
                    File.Move(tempPath, path);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(25);
            }
        }
    }

    private static List<string> NormalizeDistinct(IEnumerable<string>? values)
    {
        return (values ?? Enumerable.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(path))
            File.WriteAllText(path, "[]");
    }

    private sealed class BanEntryDto
    {
        public ulong UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public List<string> CharacterNames { get; set; } = new();
        public List<string> KnownIps { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string BannedBy { get; set; } = string.Empty;
        public DateTime BannedAtUtc { get; set; }
    }

    private sealed class IpHistoryEntryDto
    {
        public ulong UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public List<string> CharacterNames { get; set; } = new();
        public List<string> KnownIps { get; set; } = new();
        public DateTime LastSeenUtc { get; set; }
    }
}
