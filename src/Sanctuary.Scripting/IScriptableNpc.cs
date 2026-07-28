namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    ulong Guid { get; }
    string? Name { get; set; }
    IScriptableZone Zone { get; }

    void Say(string message);
    void SayLocalized(int stringId);
}
