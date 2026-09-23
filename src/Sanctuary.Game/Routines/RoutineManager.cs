using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Routines;

public sealed class RoutineManager
{
    private sealed class RoutineEntry
    {
        public readonly IRoutine Routine;
        public readonly Cadence Cadence;

        public RoutineEntry(IRoutine routine, Cadence cadence)
        {
            Routine = routine;
            Cadence = cadence;
        }
    }

    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, RoutineEntry> _routines = new();

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
            End(name, routine);
            return;
        }

        var entry = new RoutineEntry(routine, cadence);

        while (true)
        {
            if (_routines.TryGetValue(name, out var previous))
            {
                if (!_routines.TryUpdate(name, entry, previous))
                    continue;

                lock (previous)
                    End(name, previous.Routine);

                return;
            }

            if (_routines.TryAdd(name, entry))
                return;
        }
    }

    public void SetRoutine(string name, Func<bool> onStep, Cadence cadence, Action? onStart = null, Action? onEnd = null)
    {
        SetRoutine(name, new DelegateRoutine(onStart, onStep, onEnd), cadence);
    }

    public void Cancel(string name)
    {
        if (_routines.TryRemove(name, out var entry))
            lock (entry)
                End(name, entry.Routine);
    }

    public void OnTick() => Step(Cadence.Tick);
    public void OnSecond() => Step(Cadence.Second);

    private void Step(Cadence cadence)
    {
        foreach (var (name, entry) in _routines)
        {
            if (entry.Cadence != cadence)
                continue;

            lock (entry)
            {
                bool done;

                try
                {
                    done = entry.Routine.OnStep();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Routine '{name}' threw and will be removed.", name);
                    done = true;
                }

                if (done && _routines.TryRemove(new(name, entry)))
                    End(name, entry.Routine);
            }
        }
    }

    private void End(string name, IRoutine routine)
    {
        try
        {
            routine.OnEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routine '{name}' threw during OnEnd.", name);
        }
    }
}
