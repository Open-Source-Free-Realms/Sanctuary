using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sanctuary.Game.Authoring;

public sealed record ScriptTemplateParam(string Key, string Type, string Default);

/// <summary>
/// A script library entry: a shipped template (Resources/ScriptTemplates) or a GM-saved script.
/// Both describe themselves with header comments:
/// <code>
/// -- @name Greeter
/// -- @description Says a line when clicked
/// -- @param text string Hello there!
/// </code>
/// and use <c>{{key}}</c> placeholders in the body.
/// </summary>
public sealed partial record ScriptTemplate
{
    public static readonly string[] ParamTypes = ["string", "int", "float", "item", "npc", "anim"];

    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Source { get; init; }
    public bool IsTemplate { get; init; }
    public IReadOnlyList<ScriptTemplateParam> Params { get; init; } = [];

    [GeneratedRegex(@"^--\s*@(\w+)\s*(.*)$")]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex NameRegex();

    public static bool IsValidName(string name) => NameRegex().IsMatch(name);

    /// <summary>
    /// Reads the header comments. <paramref name="name"/> and <paramref name="description"/> override the header's.
    /// </summary>
    public static ScriptTemplate Parse(string source, string? name, string? description, bool isTemplate)
    {
        string? headerName = null;
        string? headerDescription = null;
        var parameters = new List<ScriptTemplateParam>();

        foreach (var rawLine in source.Split('\n'))
        {
            var match = HeaderRegex().Match(rawLine.Trim());

            if (!match.Success)
                continue;

            var value = match.Groups[2].Value.Trim();

            switch (match.Groups[1].Value.ToLowerInvariant())
            {
                case "name":
                    headerName = value;
                    break;

                case "description":
                    headerDescription = value;
                    break;

                case "param":
                    // "<key> <type> <default...>"
                    var parts = value.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 2 && ParamTypes.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                        parameters.Add(new ScriptTemplateParam(parts[0], parts[1].ToLowerInvariant(), parts.Length == 3 ? parts[2] : string.Empty));
                    break;
            }
        }

        return new ScriptTemplate
        {
            Name = name ?? headerName ?? string.Empty,
            Description = description ?? headerDescription ?? string.Empty,
            Source = source,
            IsTemplate = isTemplate,
            Params = parameters
        };
    }

    /// <summary>
    /// Fills the placeholders. Missing values take the header default; string values are escaped
    /// for a Lua string literal and the other types must be numbers. A placeholder without a
    /// matching @param fails: Lua would otherwise compile "{{hits}}" as a nested table and the
    /// script would only break when it runs.
    /// </summary>
    public bool TryRender(IReadOnlyDictionary<string, string> values, out string source, out string? error)
    {
        source = string.Empty;
        error = null;

        var unknown = values.Keys.FirstOrDefault(key => !Params.Any(param => param.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));

        if (unknown is not null)
        {
            error = $"{Name} has no parameter '{unknown}'. Parameters: {string.Join(", ", Params.Select(x => x.Key))}.";
            return false;
        }

        var rendered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in Params)
        {
            var value = values.FirstOrDefault(x => x.Key.Equals(param.Key, StringComparison.OrdinalIgnoreCase)).Value ?? param.Default;

            if (!TryFormat(param, value, out var formatted))
            {
                error = $"'{param.Key}' must be {(param.Type == "float" ? "a number" : "a whole number")}, not '{value}'.";
                return false;
            }

            rendered[param.Key] = formatted;
        }

        string? undeclared = null;

        var filled = PlaceholderRegex().Replace(Source, match =>
        {
            if (rendered.TryGetValue(match.Groups[1].Value, out var value))
                return value;

            undeclared ??= match.Groups[1].Value;
            return match.Value;
        });

        if (undeclared is not null)
        {
            error = $"{{{{{undeclared}}}}} has no \"-- @param {undeclared} <type> <default>\" line to fill it. " +
                $"For a nested Lua table, write {{ {{{undeclared}}} }} instead.";
            return false;
        }

        source = filled;

        return true;
    }

    private static bool TryFormat(ScriptTemplateParam param, string value, out string formatted)
    {
        formatted = string.Empty;

        switch (param.Type)
        {
            case "string":
                formatted = EscapeLuaString(value);
                return true;

            case "float":
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return false;

                formatted = number.ToString(CultureInfo.InvariantCulture);
                return true;

            default:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    return false;

                formatted = integer.ToString(CultureInfo.InvariantCulture);
                return true;
        }
    }

    /// <summary>
    /// For placement inside a "double-quoted" Lua string literal in the template.
    /// </summary>
    private static string EscapeLuaString(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': builder.Append(@"\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\'': builder.Append("\\'"); break;
                case '\n': builder.Append(@"\n"); break;
                case '\r': break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }
}
