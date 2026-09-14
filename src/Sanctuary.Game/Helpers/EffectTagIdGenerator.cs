using System.Threading;

namespace Sanctuary.Game.Helpers;

public static class EffectTagIdGenerator
{
    private static int _counter = 5000;

    public static int Next() => Interlocked.Increment(ref _counter);
}
