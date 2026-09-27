namespace Sanctuary.Game.Routines;

public sealed class RepeatRoutine : IRoutine
{
    private readonly IRoutine _routine;
    private readonly int? _count;

    private int _completed;
    private bool _innerRunning;

    public RepeatRoutine(IRoutine routine, int? count = null)
    {
        _routine = routine;
        _count = count;
    }

    public void OnStart()
    {
        _completed = 0;
        _innerRunning = _count is null or > 0;

        if (_innerRunning)
            _routine.OnStart();
    }

    public bool OnStep()
    {
        if (!_innerRunning)
            return true;

        if (!_routine.OnStep())
            return false;

        _routine.OnEnd();
        _innerRunning = false;
        _completed++;

        if (_count is not null && _completed >= _count)
            return true;

        _routine.OnStart();
        _innerRunning = true;

        return false;
    }

    public void OnEnd()
    {
        if (!_innerRunning)
            return;

        _innerRunning = false;
        _routine.OnEnd();
    }
}
