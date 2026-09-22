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
        _routines.RemoveAll(routine => routine.OnStep());
        return _routines.Count == 0;
    }
}
