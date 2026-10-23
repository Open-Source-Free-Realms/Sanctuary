namespace Sanctuary.Game.Routines;

public sealed class DelayedRoutine : IRoutine
{
    private readonly IRoutine _sequence;

    public DelayedRoutine(IRoutine routine, double seconds)
    {
        _sequence = new SequentialRoutine([new WaitRoutine(seconds), routine]);
    }

    public void OnStart() => _sequence.OnStart();
    public bool OnStep() => _sequence.OnStep();
    public void OnEnd() => _sequence.OnEnd();
}
