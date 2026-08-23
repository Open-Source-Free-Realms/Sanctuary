using System;
using System.Diagnostics;

namespace Sanctuary.Core.Actions;

public sealed class WaitAction : IAction
{
    private readonly double _seconds;
    private readonly Stopwatch _stopwatch = new();

    public WaitAction(double seconds) => _seconds = seconds;

    public void OnStart() => _stopwatch.Restart();
    public bool OnTick() => _stopwatch.Elapsed.TotalSeconds >= _seconds;
}
