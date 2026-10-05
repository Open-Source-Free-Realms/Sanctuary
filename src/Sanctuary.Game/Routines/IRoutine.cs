namespace Sanctuary.Game.Routines;

public interface IRoutine
{
    void OnStart() { }
    bool OnStep() => false;
    void OnEnd() { }
}
