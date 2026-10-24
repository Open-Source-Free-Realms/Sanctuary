using System;

namespace Sanctuary.Game.Routines;

public abstract class Routine
{
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        OnStart();
    }

    // NOTE: I don't really like passing through the time delta, but this is to make things
    // deterministic in some of the timing-based routines. The manager will pass down time
    // delta so it follows an arbitrary tick clock.    
    public bool Step(TimeSpan delta)
    {
        if (!IsRunning)
            return true;

        if (!OnStep(delta))
            return false;

        if (IsRunning)
        {
            IsRunning = false;
            OnEnd();
        }

        return true;
    }

    public void Interrupt()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        OnInterrupted();
    }

    protected virtual void OnStart() { }

    protected abstract bool OnStep(TimeSpan delta);

    protected virtual void OnEnd() { }

    protected virtual void OnInterrupted() => OnEnd();
}
