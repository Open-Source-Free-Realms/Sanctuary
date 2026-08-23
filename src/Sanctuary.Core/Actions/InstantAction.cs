using System;

namespace Sanctuary.Core.Actions;

public sealed class InstantAction : IAction
{
    private readonly Action _action;

    public InstantAction(Action action)
    {
        _action = action;
    }

    public void OnStart() => _action();
    public bool OnTick() => true;
}
