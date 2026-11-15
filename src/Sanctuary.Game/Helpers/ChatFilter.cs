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

    // apostrophes are kept so contractions don't join into other words ("he'll" -> "hell")
    private const string Apostrophes = "'’‘`";

    // replaces blacklisted text with #, skipping anything the whitelist allows
    public static string Censor(string message, ChatFilterList blacklist, ChatFilterList whitelist, ILogger? logger = null)
    {
        // nothing to do if the message is empty or the blacklist is off
        if (string.IsNullOrEmpty(message) || !blacklist.IsActive)
            return message;

        // copy of the message without accents or punctuation, so "b.a.d" and "bád" are checked as "bad"
        var stripped = Strip(message, out var strippedToOriginal);

        // find every blacklist match
        var blocked = FindMatches(message, stripped, strippedToOriginal, blacklist.Patterns, logger, out _);

        if (blocked.Count == 0)
            return message;

        // find every whitelist match, only needed once something is blocked
        var allowed = whitelist.IsActive
            ? FindMatches(message, stripped, strippedToOriginal, whitelist.Patterns, logger, out _)
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

    // true if a name (character or guild) has blacklisted text the whitelist doesn't allow
    public static bool IsBlocked(string name, IResourceManager resourceManager, ILogger? logger = null)
        => IsBlocked(name, resourceManager.ChatBlacklist.Current, resourceManager.ChatWhitelist.Current, logger);

    // same check, with the lists passed in
    public static bool IsBlocked(string name, ChatFilterList blacklist, ChatFilterList whitelist, ILogger? logger = null)
    {
        // blacklist off means names aren't checked
        if (string.IsNullOrEmpty(name) || !blacklist.IsActive)
            return false;

        var stripped = Strip(name, out var strippedToOriginal);
        var blocked = FindMatches(name, stripped, strippedToOriginal, blacklist.Patterns, logger, out var timedOut);

        // a pattern that timed out can't clear the name, so treat it as blocked
        if (timedOut)
            return true;

        if (blocked.Count == 0)
            return false;

        var allowed = whitelist.IsActive
            ? FindMatches(name, stripped, strippedToOriginal, whitelist.Patterns, logger, out _)
            : [];

        // blocked if any match isn't inside a whitelist match
        return blocked.Exists(b => !allowed.Exists(a => a.Start <= b.Start && b.End <= a.End));
    }

    // removes accents, punctuation and symbols, and records where each kept character came from
    internal static string Strip(string message, out int[] strippedToOriginal)
    {
        var builder = new StringBuilder(message.Length);
        var map = new List<int>(message.Length);

        for (var i = 0; i < message.Length; i++)
        {
            // turn accented and full-width characters into plain letters, "á" -> "a", "ｄ" -> "d" (the accent is dropped below)
            var chars = message[i] < 0x80 || char.IsSurrogate(message[i])
                ? message[i].ToString()
                : message[i].ToString().Normalize(NormalizationForm.FormKD);

            foreach (var c in chars)
            {
                if (IsSeparator(c))
                    continue;

                builder.Append(c);

                // remember where this character came from so matches can be mapped back
                map.Add(i);
            }
        }

        strippedToOriginal = map.ToArray();
        return builder.ToString();
    }

    // true if the character should be removed by Strip
    private static bool IsSeparator(char c)
    {
        // letters, digits, spaces and apostrophes are never stripped
        if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || Apostrophes.Contains(c))
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
    // timedOut is true if any pattern took too long and was skipped
    private static List<(int Start, int End)> FindMatches(string message, string stripped, int[] strippedToOriginal,
        IReadOnlyList<Regex> patterns, ILogger? logger, out bool timedOut)
    {
        var ranges = new List<(int Start, int End)>();
        timedOut = false;

        // the stripped copy only needs checking if something was stripped or replaced
        var checkStripped = stripped != message;

        // loop each regex entry
        foreach (var pattern in patterns)
        {
            try
            {
                // check the message as typed, catches cases stripping would miss ("hi.bad" -> "hibad", "b@d")
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
                // pattern took too long on this text, skip it rather than stall the server
                timedOut = true;
                logger?.LogWarning("Chat filter pattern \"{pattern}\" timed out on \"{text}\".", pattern, message);
            }
        }

        return ranges;
    }
}
