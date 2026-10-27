using System;

namespace Sanctuary.Game.Routines;

public sealed class TimeoutRoutine : Routine
{
    private readonly Routine _routine;
    private readonly TimeSpan _timeout;
    private TimeSpan _elapsed;

    public TimeoutRoutine(Routine routine, double timeoutSeconds)
    {
        _routine = routine;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    protected override void OnStart()
    {
        _elapsed = TimeSpan.Zero;
        _routine.Start();
    }

    protected override bool OnStep(TimeSpan delta)
    {
        if (_routine.Step(delta))
            return true;

        _elapsed += delta;

        if (_elapsed < _timeout)
            return false;

        _routine.Interrupt();
        return true;
    }

    protected override void OnInterrupted() => _routine.Interrupt();
}
