using System;

namespace Sanctuary.Game.Routines;

public sealed class InstantRoutine : IRoutine
{
    private readonly Action _action;

    public InstantRoutine(Action action)
    {
        _action = action;
    }

    public void OnStart() => _action();
    public bool OnStep() => true;
}
