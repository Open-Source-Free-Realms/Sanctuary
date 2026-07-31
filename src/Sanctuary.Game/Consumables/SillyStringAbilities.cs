namespace Sanctuary.Game.Consumables;

// Silly String cans (ClientItemDefinitions.json CategoryId 29) - sprays the nearest other player.
// Effect ids are real, from ActorCompositeEffectDefinitions.xml's PFX_silly-string_beam_<color>_p2p family.
public static class SillyStringAbilities
{
    private static readonly (string Color, int EffectId)[] _byColor =
    [
        ("blue",   15825),
        ("red",    15818),
        ("green",  15826),
        ("orange", 15827),
        ("yellow", 15828),
        ("black",  15905),
    ];

    public static bool TryResolve(string comment, out int effectId)
    {
        var lower = comment.ToLowerInvariant();

        if (lower.Contains("silly string"))
        {
            foreach (var (color, id) in _byColor)
            {
                if (lower.Contains(color))
                {
                    effectId = id;
                    return true;
                }
            }
        }

        effectId = 0;
        return false;
    }
}
