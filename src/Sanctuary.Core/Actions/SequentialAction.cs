using System.Collections.Generic;

namespace Sanctuary.Core.Actions;

public sealed class SequentialAction : IAction
{
    private readonly Queue<IAction> _actions;
    private IAction? _current;

    public SequentialAction(IEnumerable<IAction> actions) => _actions = new Queue<IAction>(actions);

    public void OnStart() => Advance();

    public bool OnTick()
    {
        if (_current is null) return true;
        if (!_current.OnTick()) return false;
        return Advance();
    }

    private bool Advance()
    {
        if (!_actions.TryDequeue(out _current)) return true;
        _current.OnStart();
        return false;
    }
}
