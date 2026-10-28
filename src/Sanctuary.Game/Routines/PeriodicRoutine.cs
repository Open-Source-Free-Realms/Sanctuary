using System;

namespace Sanctuary.Game.Routines;

public sealed class PeriodicRoutine : Routine
{
    private readonly RepeatRoutine _repeat;

    public PeriodicRoutine(Routine routine, double period, double? totalDuration = null)
    {
        int? count = totalDuration is { } total ? (int)Math.Floor(total / period + 1e-9) : null;
        _repeat = new RepeatRoutine(new ParallelRoutine(routine, new WaitRoutine(period)), count);
    }

    protected override void OnStart() => _repeat.Start();

    protected override bool OnStep(TimeSpan delta) => _repeat.Step(delta);

    protected override void OnInterrupted() => _repeat.Interrupt();
}
