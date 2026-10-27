using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Routines;

public sealed class ParallelRoutine : Routine
{
    private readonly Routine[] _routines;

    public ParallelRoutine(params IEnumerable<Routine> routines) => _routines = routines.ToArray();

    protected override void OnStart()
    {
        foreach (var routine in _routines)
        {
            routine.Start();

            if (!IsRunning)
                return;
        }
    }

    protected override bool OnStep(TimeSpan delta)
    {
        var allDone = true;

        foreach (var routine in _routines)
        {
            if (!routine.IsRunning)
                continue;

            if (!routine.Step(delta))
                allDone = false;

            if (!IsRunning)
                return true;
        }

        return allDone;
    }

    protected override void OnInterrupted()
    {
        foreach (var routine in _routines)
            routine.Interrupt();
    }
}
