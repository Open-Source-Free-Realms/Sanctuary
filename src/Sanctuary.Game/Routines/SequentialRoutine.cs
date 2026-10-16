using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Routines;

public sealed class SequentialRoutine : IRoutine
{
    private readonly IRoutine[] _routines;
    private readonly Queue<IRoutine> _pending = new();

    private IRoutine? _current;

    public SequentialRoutine(IEnumerable<IRoutine> routines) => _routines = routines.ToArray();

    public void OnStart()
    {
        _pending.Clear();

        foreach (var routine in _routines)
            _pending.Enqueue(routine);

        _current = null;

        Advance();
    }

    public bool OnStep()
    {
        if (_current is null) return true;
        if (!_current.OnStep()) return false;
        return Advance();
    }

    public void OnEnd()
    {
        _current?.OnEnd();
        _current = null;

        _pending.Clear();
    }

    private bool Advance()
    {
        _current?.OnEnd();

        if (!_pending.TryDequeue(out _current)) return true;

        _current.OnStart();
        return false;
    }
}
