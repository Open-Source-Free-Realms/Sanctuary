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

        StartNext();
    }

    public bool OnStep()
    {
        while (_current is not null)
        {
            if (!_current.OnStep())
            {
                return false;
            }

            _current.OnEnd();
            StartNext();
        }

        return true;
    }

    public void OnEnd()
    {
        _current?.OnEnd();
        _current = null;

        _pending.Clear();
    }

    private void StartNext()
    {
        if (_pending.TryDequeue(out _current))
            _current.OnStart();
    }
}
