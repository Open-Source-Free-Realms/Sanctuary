using System.Text;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Escaping for free text inside GM panel lines and the commands the panel sends back, which are
/// single '|'-separated chat lines: '\' becomes "\\", a newline becomes "\n" and '|' becomes "\p".
/// </summary>
public static class PanelTextHelper
{
    public static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': builder.Append(@"\\"); break;
                case '\n': builder.Append(@"\n"); break;
                case '|': builder.Append(@"\p"); break;
                case '\r': break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }

    public static string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 == text.Length)
            {
                builder.Append(text[i]);
                continue;
            }

            switch (text[++i])
            {
                case 'n': builder.Append('\n'); break;
                case 'p': builder.Append('|'); break;
                // Chat commands split on spaces, so the panel sends these as escapes inside script chunks.
                case 's': builder.Append(' '); break;
                case 't': builder.Append('\t'); break;
                case '\\': builder.Append('\\'); break;
                default: builder.Append('\\').Append(text[i]); break;
            }
        }

        return builder.ToString();
    }
}
