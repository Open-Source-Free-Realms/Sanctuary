using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Routines;

public sealed class SequentialRoutine : Routine
{
    private readonly Routine[] _routines;
    private int _index;

    public SequentialRoutine(params IEnumerable<Routine> routines) => _routines = routines.ToArray();

    protected override void OnStart()
    {
        _index = 0;
        StartCurrent();
    }

    protected override bool OnStep(TimeSpan delta)
    {
        while (_index < _routines.Length)
        {
            if (!_routines[_index].Step(delta))
                return false;

            if (!IsRunning)
                return true;

            _index++;
            StartCurrent();
            delta = TimeSpan.Zero;
        }

        return true;
    }

    protected override void OnInterrupted()
    {
        if (_index < _routines.Length)
            _routines[_index].Interrupt();
    }

    private void StartCurrent()
    {
        if (_index < _routines.Length)
            _routines[_index].Start();
    }
}
