namespace Sanctuary.Core.Helpers;

public static class CharacterNameHelper
{
    public static bool ContainsIllegalCharacters(string name)
    {
        foreach (var character in name)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '\'' or ' ')
                continue;

            return true;
        }

        return false;
    }
}
