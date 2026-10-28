using System;

namespace Sanctuary.Game.Routines;

public sealed class DelayedRoutine : Routine
{
    private readonly SequentialRoutine _sequence;

    public DelayedRoutine(Routine routine, double seconds)
    {
        _sequence = new SequentialRoutine(new WaitRoutine(seconds), routine);
    }

    protected override void OnStart() => _sequence.Start();

    protected override bool OnStep(TimeSpan delta) => _sequence.Step(delta);

    protected override void OnInterrupted() => _sequence.Interrupt();
}
