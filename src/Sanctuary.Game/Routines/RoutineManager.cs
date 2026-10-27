using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Routines;

public sealed class RoutineManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Routine> _routines = new();

    private readonly TimeSpan _tickDelta;

    public RoutineManager(ILogger logger, TimeSpan tickDelta)
    {
        _logger = logger;
        _tickDelta = tickDelta;
    }

    public void SetRoutine(string name, Routine routine)
    {
        lock (routine)
        {
            Routine? previous = null;

            _routines.AddOrUpdate(name, routine, (_, existing) =>
            {
                previous = existing;
                return routine;
            });

            if (previous is not null)
                lock (previous)
                    Interrupt(name, previous);

            try
            {
                routine.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Routine '{name}' threw during Start and will not be scheduled.", name);

                if (_routines.TryRemove(new(name, routine)))
                    Interrupt(name, routine);
            }
        }
    }

    public void SetRoutine(string name, Func<bool> onStep, Action? onStart = null, Action? onEnd = null, Action? onInterrupted = null)
    {
        SetRoutine(name, new DelegateRoutine(onStart, onStep, onEnd, onInterrupted));
    }

    public void Cancel(string name)
    {
        if (_routines.TryRemove(name, out var routine))
            lock (routine)
                Interrupt(name, routine);
    }

    public void OnTick()
    {
        foreach (var (name, routine) in _routines)
        {
            lock (routine)
            {
                bool done;

                try
                {
                    done = routine.Step(_tickDelta);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Routine '{name}' threw and will be removed.", name);
                    Interrupt(name, routine);
                    done = true;
                }

                if (done)
                    _routines.TryRemove(new(name, routine));
            }
        }
    }

    private void Interrupt(string name, Routine routine)
    {
        try
        {
            routine.Interrupt();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routine '{name}' threw while being interrupted.", name);
        }
    }
}
