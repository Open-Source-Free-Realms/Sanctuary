using System;

namespace Sanctuary.Game.Routines;

public sealed class DelegateRoutine : IRoutine
{
    private readonly Action _onStart;
    private readonly Func<bool> _onStep;
    private readonly Action _onEnd;

    public DelegateRoutine(Action? onStart = null, Func<bool>? onStep = null, Action? onEnd = null)
    {
        _onStart = onStart ?? (() => { });
        _onStep = onStep ?? (() => true);
        _onEnd = onEnd ?? (() => { });
    }

    public void OnStart() => _onStart();
    public bool OnStep() => _onStep();
    public void OnEnd() => _onEnd();
}
