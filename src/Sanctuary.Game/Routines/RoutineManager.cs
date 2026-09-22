using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Routines;

public sealed class RoutineManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, (IRoutine Routine, Cadence Cadence)> _routines = new();

    public RoutineManager(ILogger logger)
    {
        _logger = logger;
    }

    public void SetRoutine(string name, IRoutine routine, Cadence cadence)
    {
        try
        {
            routine.OnStart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routine '{name}' threw during OnStart and will not be scheduled.", name);
            return;
        }

        _routines[name] = (routine, cadence);
    }

    public void SetRoutine(string name, Func<bool> onStep, Cadence cadence, Action? onStart = null)
    {
        SetRoutine(name, new DelegateRoutine(onStart, onStep), cadence);
    }

    public void Cancel(string name) => _routines.TryRemove(name, out _);

    public void OnTick() => Step(Cadence.Tick);
    public void OnSecond() => Step(Cadence.Second);

    private void Step(Cadence cadence)
    {
        List<KeyValuePair<string, (IRoutine Routine, Cadence Cadence)>>? finished = null;

        foreach (var entry in _routines)
        {
            if (entry.Value.Cadence != cadence)
                continue;

            bool done;

            try
            {
                done = entry.Value.Routine.OnStep();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Routine '{name}' threw and will be removed.", entry.Key);
                done = true;
            }

            if (done)
                (finished ??= new()).Add(entry);
        }

        if (finished is not null)
            foreach (var entry in finished)
                _routines.TryRemove(entry);
    }
}
