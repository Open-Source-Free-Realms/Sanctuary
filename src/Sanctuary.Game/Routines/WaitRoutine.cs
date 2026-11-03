using System;

namespace Sanctuary.Game.Routines;

public sealed class WaitRoutine : Routine
{
    private readonly TimeSpan _duration;
    private TimeSpan _elapsed;

    public WaitRoutine(double seconds) => _duration = TimeSpan.FromSeconds(seconds);

    protected override void OnStart() => _elapsed = TimeSpan.Zero;

    protected override bool OnStep(TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= _duration;
    }
}
