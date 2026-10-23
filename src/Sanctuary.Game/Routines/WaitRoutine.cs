using System.Diagnostics;

namespace Sanctuary.Game.Routines;

public sealed class WaitRoutine : IRoutine
{
    private readonly double _seconds;
    private readonly Stopwatch _stopwatch = new();

    public WaitRoutine(double seconds) => _seconds = seconds;

    public void OnStart() => _stopwatch.Restart();
    public bool OnStep() => _stopwatch.Elapsed.TotalSeconds >= _seconds;
}
