using System;

namespace Sanctuary.Game.Routines;

public sealed class DelegateRoutine : IRoutine
{
    private readonly Action _onStart;
    private readonly Func<bool> _onStep;

    public DelegateRoutine(Action? onStart = null, Func<bool>? onStep = null)
    {
        _onStart = onStart ?? (() => { });
        _onStep = onStep ?? (() => true);
    }

    public void OnStart() => _onStart();
    public bool OnStep() => _onStep();
}
