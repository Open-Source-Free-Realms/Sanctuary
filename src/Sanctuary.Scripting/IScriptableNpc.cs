namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    public ulong Guid { get; init; }
    public string? Name { get; set; }
    public IScriptableZone Zone { get; }

    void Say(string message);
    void SayLocalized(int stringId);
}
