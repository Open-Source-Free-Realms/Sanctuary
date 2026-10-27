namespace Sanctuary.Game.ChatCommands;

public static class ChatCommandText
{
    /// <summary>
    /// Returns the text after the first <paramref name="skipTokens"/> space-separated tokens and the single
    /// space that follows them, exactly as typed (runs of spaces and trailing spaces are kept).
    /// </summary>
    public static string Remainder(string text, int skipTokens)
    {
        var i = 0;

        for (var token = 0; token < skipTokens; token++)
        {
            while (i < text.Length && text[i] == ' ')
                i++;

            while (i < text.Length && text[i] != ' ')
                i++;
        }

        if (i < text.Length && text[i] == ' ')
            i++;

        return text[i..];
    }
}
