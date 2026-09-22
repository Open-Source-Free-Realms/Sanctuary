using System.Collections.Generic;

namespace Sanctuary.Game.Routines;

public sealed class SequentialRoutine : IRoutine
{
    private readonly Queue<IRoutine> _routines;
    private IRoutine? _current;

    public SequentialRoutine(IEnumerable<IRoutine> routines) => _routines = new Queue<IRoutine>(routines);

    public void OnStart() => Advance();

    public bool OnStep()
    {
        if (_current is null) return true;
        if (!_current.OnStep()) return false;
        return Advance();
    }

    private bool Advance()
    {
        if (!_routines.TryDequeue(out _current)) return true;
        _current.OnStart();
        return false;
    }
}
