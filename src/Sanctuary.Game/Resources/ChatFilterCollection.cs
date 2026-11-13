using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Resources;

// one chat filter list (blacklist or whitelist), built from a default file and an optional local file
public class ChatFilterCollection
{
    // safety to prevent a badly written pattern from stalling the server (denial of service)
    // for more reading, see:
    // https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices
    // https://learn.microsoft.com/en-us/dotnet/standard/base-types/backtracking-in-regular-expressions
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger _logger;
    private readonly object _lock = new();
    private volatile ChatFilterList _current = ChatFilterList.Empty;
    private bool _hasLoaded;
    private string? _defaultFilePath;
    private string? _localFilePath;

    public ChatFilterCollection(ILogger logger)
    {
        _logger = logger;
    }

    // swapped as a whole on reload, so chat never sees a half-loaded list
    public ChatFilterList Current => _current;

    // loads the default file plus the local file, returns false only if the default file can't be loaded
    public bool Load(string defaultFilePath, string localFilePath)
    {
        lock (_lock)
        {
            // remember the paths so the edit methods know where to save
            _defaultFilePath = defaultFilePath;
            _localFilePath = localFilePath;

            // the default file ships with the server, so it must exist
            if (!File.Exists(defaultFilePath))
            {
                _logger.LogError("Failed to find file \"{file}\"", defaultFilePath);
                return false;
            }

            DefaultFile defaults;

            // load the tracked default file
            try
            {
                defaults = Read<DefaultFile>(defaultFilePath) ?? new DefaultFile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse file \"{file}\".", defaultFilePath);
                return false;
            }

            LocalFile? local = null;

            // load the server owner's local file, if there is one
            if (File.Exists(localFilePath))
            {
                try
                {
                    local = Read<LocalFile>(localFilePath) ?? new LocalFile();
                }
                catch (Exception ex)
                {
                    // on a reload, keep the current list so a typo doesn't turn the filter off
                    if (_hasLoaded)
                    {
                        _logger.LogError(ex, "Failed to parse file \"{file}\". Keeping the previously loaded chat filter.", localFilePath);
                        return true;
                    }

                    // on startup, fall back to the defaults
                    _logger.LogError(ex, "Failed to parse file \"{file}\". Using only \"{defaultFile}\" until it is fixed.", localFilePath, defaultFilePath);
                }
            }

            // local Enabled overrides the default
            var enabled = local?.Enabled ?? defaults.Enabled;

            // defaults minus Remove, then Add on top (a local entry with a default's key replaces it)
            var removed = new HashSet<string>(local?.Remove ?? [], StringComparer.OrdinalIgnoreCase);
            var merged = new Dictionary<string, (Entry Entry, string File)>(StringComparer.OrdinalIgnoreCase);

            MergeEntries(merged, defaults.Entries, defaultFilePath, removed);
            MergeEntries(merged, local?.Add, localFilePath, null);

            var loadedEntries = new List<Entry>();
            var patterns = new List<Regex>();

            // loop entries in the merged list
            foreach (var (entry, file) in merged.Values)
            {
                // compile once here instead of on every message
                if (!TryCompile(entry.Pattern, out var regex, out var error))
                {
                    // invalid regex, log it and keep loading the rest
                    _logger.LogError("Skipping chat filter entry \"{key}\" in \"{file}\", invalid pattern. {error}", entry.Key, file, error);
                    continue;
                }

                loadedEntries.Add(entry);
                patterns.Add(regex);
            }

            // swap in the new list
            _current = new ChatFilterList(enabled, patterns, loadedEntries);
            _hasLoaded = true;

            return true;
        }
    }

    // turns the list on or off in the local file
    public bool SetEnabled(bool enabled, out string message)
    {
        lock (_lock)
        {
            if (!TryReadFiles(out _, out var local, out message))
                return false;

            // local Enabled overrides the default file
            local.Enabled = enabled;

            return Save(local, enabled ? "Enabled." : "Disabled.", out message);
        }
    }

    // adds an entry through the local file
    public bool AddEntry(string key, string pattern, out string message)
    {
        lock (_lock)
        {
            // reject bad regex before it reaches the file
            if (!TryCompile(pattern, out _, out var error))
            {
                message = $"Invalid pattern: {error}";
                return false;
            }

            if (!TryReadFiles(out var defaults, out var local, out message))
                return false;

            // check if the key is already in use
            var inAdd = local.Add?.Exists(x => KeyEquals(x.Key, key)) == true;
            var defaultEntry = defaults.Entries?.Find(x => KeyEquals(x.Key, key));
            var defaultRemoved = local.Remove?.Exists(x => KeyEquals(x, key)) == true;

            if (inAdd || (defaultEntry is not null && !defaultRemoved))
            {
                message = $"\"{key}\" is already in the list, remove it first.";
                return false;
            }

            // re-adding a removed default with the same pattern just takes it out of Remove
            if (defaultEntry is not null && defaultEntry.Pattern == pattern)
                local.Remove!.RemoveAll(x => KeyEquals(x, key));
            else
                (local.Add ??= []).Add(new Entry { Key = key, Pattern = pattern });

            return Save(local, $"Added \"{key}\".", out message);
        }
    }

    // removes an entry through the local file
    public bool RemoveEntry(string key, out string message)
    {
        lock (_lock)
        {
            if (!TryReadFiles(out var defaults, out var local, out message))
                return false;

            // entries added locally are deleted, default entries go into Remove
            var removedFromAdd = (local.Add?.RemoveAll(x => KeyEquals(x.Key, key)) ?? 0) > 0;
            var inDefaults = defaults.Entries?.Exists(x => KeyEquals(x.Key, key)) == true;
            var alreadyRemoved = local.Remove?.Exists(x => KeyEquals(x, key)) == true;

            if (inDefaults && !alreadyRemoved)
                (local.Remove ??= []).Add(key);

            if (!removedFromAdd && (!inDefaults || alreadyRemoved))
            {
                message = $"\"{key}\" is not in the list.";
                return false;
            }

            return Save(local, $"Removed \"{key}\".", out message);
        }
    }

    // adds valid entries to the merged list, later files replace earlier ones with the same key
    private void MergeEntries(Dictionary<string, (Entry Entry, string File)> merged, List<Entry>? entries, string file, HashSet<string>? removed)
    {
        foreach (var entry in entries ?? [])
        {
            // every entry needs a one-word key and a pattern
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(entry.Pattern))
            {
                _logger.LogError("Skipping chat filter entry in \"{file}\" with pattern \"{pattern}\", it needs a one-word Key and a Pattern.", file, entry.Pattern);
                continue;
            }

            if (removed?.Contains(entry.Key) == true)
                continue;

            merged[entry.Key] = (entry, file);
        }
    }

    // keys ignore case
    private static bool KeyEquals(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // compiles a pattern with the filter's options, returns the error if it's not valid regex
    public static bool TryCompile(string pattern, out Regex regex, out string error)
    {
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException ex)
        {
            regex = null!;
            error = ex.Message;
            return false;
        }
    }

    // reads both files for editing, refuses if the local file is broken so it isn't overwritten
    private bool TryReadFiles(out DefaultFile defaults, out LocalFile local, out string message)
    {
        defaults = new DefaultFile();
        local = new LocalFile();

        // paths are only known after Load
        if (_defaultFilePath is null || _localFilePath is null)
        {
            message = "The chat filter hasn't been loaded.";
            return false;
        }

        try
        {
            defaults = Read<DefaultFile>(_defaultFilePath) ?? new DefaultFile();
        }
        catch (Exception ex)
        {
            message = $"Couldn't read {Path.GetFileName(_defaultFilePath)}: {ex.Message}";
            return false;
        }

        // no local file yet, start from an empty one
        if (!File.Exists(_localFilePath))
        {
            message = string.Empty;
            return true;
        }

        try
        {
            local = Read<LocalFile>(_localFilePath) ?? new LocalFile();
        }
        catch (Exception ex)
        {
            message = $"{Path.GetFileName(_localFilePath)} has an error, fix it by hand first: {ex.Message}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    // writes the local file through a temp file so a crash can't leave it half written, then reloads
    private bool Save(LocalFile local, string successMessage, out string message)
    {
        var tempFilePath = _localFilePath + ".tmp";

        try
        {
            File.WriteAllText(tempFilePath, JsonSerializer.Serialize(local, JsonOptions));
            File.Move(tempFilePath, _localFilePath!, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file \"{file}\".", _localFilePath);
            message = $"Couldn't save {Path.GetFileName(_localFilePath)}: {ex.Message}";
            return false;
        }

        // apply the change right away
        Load(_defaultFilePath!, _localFilePath!);

        message = successMessage;
        return true;
    }

    // reads and deserializes a json file, comments allowed
    private static T? Read<T>(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return JsonSerializer.Deserialize<T>(fileStream, JsonOptions);
    }

    // one filter entry, Key is what admins use to remove it
    public class Entry
    {
        public string Key { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    // ChatBlacklist.json / ChatWhitelist.json
    private class DefaultFile
    {
        public bool Enabled { get; set; }
        public List<Entry>? Entries { get; set; }
    }

    // ChatBlacklist.local.json / ChatWhitelist.local.json
    private class LocalFile
    {
        public bool? Enabled { get; set; }
        public List<Entry>? Add { get; set; }
        public List<string>? Remove { get; set; }
    }
}

// a loaded list, read by chat on every message
public sealed class ChatFilterList
{
    public static readonly ChatFilterList Empty = new(false, []);

    public ChatFilterList(bool enabled, IReadOnlyList<Regex> patterns, IReadOnlyList<ChatFilterCollection.Entry>? entries = null)
    {
        Enabled = enabled;
        Patterns = patterns;
        Entries = entries ?? [];
    }

    public bool Enabled { get; }
    public IReadOnlyList<Regex> Patterns { get; }

    // the entries behind Patterns, same order, used for listing
    public IReadOnlyList<ChatFilterCollection.Entry> Entries { get; }

    // on and has something to check
    public bool IsActive => Enabled && Patterns.Count > 0;
}
