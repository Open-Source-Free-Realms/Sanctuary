namespace Sanctuary.Scripting;

public interface IScriptNpc
{
    public ulong Guid { get; init; }
    public string? Name { get; set; }

    void Say(string message);
}
