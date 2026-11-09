using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Helpers;

// censors chat messages using the blacklist and whitelist
public static class ChatFilter
{
    public const char CensorChar = '#';

    // symbols players type in place of letters (@ = a, $ = s, ! and | = i), kept so patterns can match them
    private const string LetterLikeSymbols = "@$!|";

    // replaces blacklisted text with #, skipping anything the whitelist allows
    public static string Censor(string message, ChatFilterList blacklist, ChatFilterList whitelist, ILogger? logger = null)
    {
        // nothing to do if the message is empty or the blacklist is off
        if (string.IsNullOrEmpty(message) || !blacklist.IsActive)
            return message;

        // copy of the message without punctuation, so "b.a.d" is checked as "bad"
        var stripped = Strip(message, out var strippedToOriginal);

        // find every blacklist match
        var blocked = FindMatches(message, stripped, strippedToOriginal, blacklist.Patterns, logger);

        if (blocked.Count == 0)
            return message;

        // find every whitelist match, only needed once something is blocked
        var allowed = whitelist.IsActive
            ? FindMatches(message, stripped, strippedToOriginal, whitelist.Patterns, logger)
            : [];

        var result = message.ToCharArray();
        var censored = false;

        // loop blacklist matches
        foreach (var (start, end) in blocked)
        {
            // skip the match if it sits entirely inside a whitelist match
            if (allowed.Exists(x => x.Start <= start && end <= x.End))
                continue;

            // replace each character with #, leaving spaces so the message keeps its shape
            for (var i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(result[i]))
                    result[i] = CensorChar;
            }

            censored = true;
        }

        // only build a new string if something changed
        return censored ? new string(result) : message;
    }

    // removes punctuation/symbols and records where each kept character came from
    internal static string Strip(string message, out int[] strippedToOriginal)
    {
        var builder = new StringBuilder(message.Length);
        var map = new List<int>(message.Length);

        for (var i = 0; i < message.Length; i++)
        {
            if (IsSeparator(message[i]))
                continue;

            builder.Append(message[i]);

            // remember where this character came from so matches can be mapped back
            map.Add(i);
        }

        strippedToOriginal = map.ToArray();
        return builder.ToString();
    }

    // true if the character should be removed by Strip
    private static bool IsSeparator(char c)
    {
        // letters, digits, spaces and letter-like symbols are never stripped
        if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || LetterLikeSymbols.Contains(c))
            return false;

        // strip punctuation, symbols, accents/combining marks and invisible characters (zero-width spaces etc.)
        switch (char.GetUnicodeCategory(c))
        {
            case UnicodeCategory.ConnectorPunctuation:
            case UnicodeCategory.DashPunctuation:
            case UnicodeCategory.OpenPunctuation:
            case UnicodeCategory.ClosePunctuation:
            case UnicodeCategory.InitialQuotePunctuation:
            case UnicodeCategory.FinalQuotePunctuation:
            case UnicodeCategory.OtherPunctuation:
            case UnicodeCategory.MathSymbol:
            case UnicodeCategory.CurrencySymbol:
            case UnicodeCategory.ModifierSymbol:
            case UnicodeCategory.OtherSymbol:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.Format:
            case UnicodeCategory.Control:
                return true;

            default:
                return false;
        }
    }

    // runs each pattern on the original and stripped message, returns matches as [Start, End) ranges in the original
    private static List<(int Start, int End)> FindMatches(string message, string stripped, int[] strippedToOriginal,
        IReadOnlyList<Regex> patterns, ILogger? logger)
    {
        var ranges = new List<(int Start, int End)>();

        // the stripped copy only needs checking if something was actually stripped
        var checkStripped = stripped.Length != message.Length;

        // loop each regex entry
        foreach (var pattern in patterns)
        {
            try
            {
                // check the message as typed, catches cases stripping would miss ("hi.bad" -> "hibad")
                foreach (Match match in pattern.Matches(message))
                {
                    if (match.Length > 0)
                        ranges.Add((match.Index, match.Index + match.Length));
                }

                if (!checkStripped)
                    continue;

                // check the stripped copy and map the match back, so the punctuation inside it is censored too
                foreach (Match match in pattern.Matches(stripped))
                {
                    if (match.Length > 0)
                        ranges.Add((strippedToOriginal[match.Index], strippedToOriginal[match.Index + match.Length - 1] + 1));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // pattern took too long on this message, skip it rather than stall the server
                logger?.LogWarning("Chat filter pattern \"{pattern}\" timed out and was skipped for a message.", pattern);
            }
        }

        return ranges;
    }
}
