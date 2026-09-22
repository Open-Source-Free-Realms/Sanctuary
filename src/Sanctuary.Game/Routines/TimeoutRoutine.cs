using System.Diagnostics;

namespace Sanctuary.Game.Routines;

public sealed class TimeoutRoutine : IRoutine
{
    private readonly IRoutine _routine;
    private readonly double _timeoutSeconds;
    private readonly Stopwatch _stopwatch = new();

    public TimeoutRoutine(IRoutine routine, double timeoutSeconds)
    {
        _routine = routine;
        _timeoutSeconds = timeoutSeconds;
    }

    public void OnStart()
    {
        _stopwatch.Restart();
        _routine.OnStart();
    }

    public bool OnStep()
    {
        return _stopwatch.Elapsed.TotalSeconds >= _timeoutSeconds || _routine.OnStep();
    }
}
