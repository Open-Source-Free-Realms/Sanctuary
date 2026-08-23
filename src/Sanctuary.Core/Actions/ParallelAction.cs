using System.Collections.Generic;

namespace Sanctuary.Core.Actions;

public sealed class ParallelAction : IAction
{
    private readonly List<IAction> _actions;

    public ParallelAction(params IAction[] actions)
    {
        _actions = new List<IAction>(actions);
    }

    public void OnStart()
    {
        foreach (var action in _actions)
        {
            action.OnStart();
        }
    }

    public bool OnTick()
    {
        _actions.RemoveAll(action => action.OnTick());
        return _actions.Count == 0;
    }
}
