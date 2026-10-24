using System;

namespace Sanctuary.Game.Routines;

public sealed class RepeatRoutine : Routine
{
    private readonly Routine _routine;
    private readonly int? _count;
    private int _completed;

    public RepeatRoutine(Routine routine, int? count = null)
    {
        _routine = routine;
        _count = count;
    }

    protected override void OnStart()
    {
        _completed = 0;

        if (_count is not <= 0)
            _routine.Start();
    }

    protected override bool OnStep(TimeSpan delta)
    {
        if (_count is <= 0)
            return true;

        if (!_routine.Step(delta))
            return false;

        if (!IsRunning)
            return true;

        _completed++;

        if (_completed >= _count)
            return true;

        _routine.Start();
        return false;
    }

    protected override void OnInterrupted() => _routine.Interrupt();
}
