using System.Collections.Generic;

using Sanctuary.Core.Actions;

namespace Sanctuary.Game.Actions;

public sealed class ActionManager
{
    private readonly Dictionary<string, IAction> _actions = new();

    public void SetAction(string slot, IAction action)
    {
        _actions[slot] = action;
        action.OnStart();
    }

    public void Cancel(string slot) => _actions.Remove(slot);

    public void Tick()
    {
        List<string>? finished = null;

        foreach (var (key, action) in _actions)
        {
            if (action.OnTick())
                (finished ??= new()).Add(key);
        }

        if (finished is not null)
            foreach (var key in finished)
                _actions.Remove(key);
    }
}
