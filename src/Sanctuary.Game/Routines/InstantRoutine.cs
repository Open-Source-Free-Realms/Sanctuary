using System;

namespace Sanctuary.Game.Routines;

public sealed class InstantRoutine : Routine
{
    private readonly Action _action;

    public InstantRoutine(Action action)
    {
        _action = action;
    }

    protected override bool OnStep(TimeSpan delta)
    {
        _action();
        return true;
    }
}
