using System.Collections.Generic;

namespace Sanctuary.Game.Routines;

public sealed class ParallelRoutine : IRoutine
{
    private readonly List<IRoutine> _routines;

    public ParallelRoutine(params IRoutine[] routines)
    {
        _routines = new List<IRoutine>(routines);
    }

    public void OnStart()
    {
        foreach (var routine in _routines)
        {
            routine.OnStart();
        }
    }

    public bool OnStep()
    {
        for (int i = _routines.Count - 1; i >= 0; i--)
        {
            if (!_routines[i].OnStep())
                continue;

            _routines[i].OnEnd();
            _routines.RemoveAt(i);
        }

        return _routines.Count == 0;
    }

    public void OnEnd()
    {
        foreach (var routine in _routines)
            routine.OnEnd();
    }
}
