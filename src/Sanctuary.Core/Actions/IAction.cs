namespace Sanctuary.Core.Actions;

public interface IAction
{
    void OnStart() { }
    bool OnTick() => false;

    // TODO: 'OnSecond'?
}
