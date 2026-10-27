using System;

namespace Sanctuary.Game.Routines;

public sealed class DelegateRoutine : Routine
{
    private readonly Action? _onStart;
    private readonly Func<bool>? _onStep;
    private readonly Action? _onEnd;
    private readonly Action? _onInterrupted;

    public DelegateRoutine(Action? onStart = null, Func<bool>? onStep = null, Action? onEnd = null, Action? onInterrupted = null)
    {
        _onStart = onStart;
        _onStep = onStep;
        _onEnd = onEnd;
        _onInterrupted = onInterrupted;
    }

    protected override void OnStart() => _onStart?.Invoke();

    protected override bool OnStep(TimeSpan delta) => _onStep?.Invoke() ?? true;

    protected override void OnEnd() => _onEnd?.Invoke();

    protected override void OnInterrupted() => (_onInterrupted ?? _onEnd)?.Invoke();
}
