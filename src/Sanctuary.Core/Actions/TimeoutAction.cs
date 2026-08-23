using System;
using System.Diagnostics;

namespace Sanctuary.Core.Actions;

public sealed class TimeoutAction : IAction
{
    private readonly IAction _action;
    private readonly double _timeoutSeconds;
    private readonly Stopwatch _stopwatch = new();

    public TimeoutAction(IAction action, double timeoutSeconds)
    {
        _action = action;
        _timeoutSeconds = timeoutSeconds;
    }

    public void OnStart()
    {
        _stopwatch.Restart();
        _action.OnStart();
    }

    public bool OnTick()
    {
        return _action.OnTick() || _stopwatch.Elapsed.TotalSeconds >= _timeoutSeconds;
    }
}
