using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Routines;

public sealed class ParallelRoutine : IRoutine
{
    private readonly IRoutine[] _routines;
    private readonly List<IRoutine> _active = new();

    public ParallelRoutine(params IRoutine[] routines)
    {
        _routines = routines.ToArray();
    }

    public void OnStart()
    {
        _active.Clear();
        _active.AddRange(_routines);

        foreach (var routine in _active)
        {
            routine.OnStart();
        }
    }

    public bool OnStep()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (!_active[i].OnStep())
                continue;

            _active[i].OnEnd();
            _active.RemoveAt(i);
        }

        return _active.Count == 0;
    }

    public void OnEnd()
    {
        foreach (var routine in _active)
            routine.OnEnd();

        _active.Clear();
    }
}
