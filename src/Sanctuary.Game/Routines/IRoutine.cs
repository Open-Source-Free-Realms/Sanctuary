namespace Sanctuary.Game.Routines;

public interface IRoutine
{
    void OnStart() { }
    bool OnStep() => false;

    // TODO: perhaps an 'OnComplete' or 'OnStop' or something, especially since
    // we will just allow action overrides in the manager for a given key.
}
