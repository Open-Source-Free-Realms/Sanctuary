using System;

namespace Sanctuary.Core.Actions;

public sealed class DelegateAction : IAction
{
    private readonly Action _onStart;
    private readonly Func<bool> _onTick;

    public DelegateAction(Action? onStart = null, Func<bool>? onTick = null)
    {
        _onStart = onStart ?? (() => { });
        _onTick = onTick ?? (() => true);
    }

    public void OnStart() => _onStart();
    public bool OnTick() => _onTick();
}
