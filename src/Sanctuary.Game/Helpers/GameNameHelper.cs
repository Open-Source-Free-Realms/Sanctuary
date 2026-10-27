using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// English names by NameId for referee tools, extracted from the client locale (ClientMods/extract_names.py).
/// </summary>
public static class GameNameHelper
{
    private static readonly string NamesFile = Path.Combine(ResourceManager.BaseDirectory, "Names.json");

    private static readonly Lazy<Dictionary<int, string>> Names = new(LoadNames);

    /// <summary>
    /// Whether Names.json was found; without it every lookup falls back.
    /// </summary>
    public static bool IsLoaded => Names.Value.Count > 0;

    public static string? Get(int nameId) => Names.Value.TryGetValue(nameId, out var name) ? name : null;

    public static string Get(int nameId, string fallback) => Get(nameId) ?? fallback;

    private static Dictionary<int, string> LoadNames()
    {
        try
        {
            if (File.Exists(NamesFile))
            {
                using var stream = File.OpenRead(NamesFile);

                return JsonSerializer.Deserialize<Dictionary<int, string>>(stream) ?? [];
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }
}
