using System;

namespace Sanctuary.Game.Routines;

public sealed class InstantRoutine : IRoutine
{
    private readonly Action _action;

    public InstantRoutine(Action action)
    {
        _action = action;
    }

    public bool OnStep()
    {
        _action();
        return true;
    }
}
