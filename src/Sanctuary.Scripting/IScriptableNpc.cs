using System.Numerics;

using Sanctuary.Core.Actions;

namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    ulong Guid { get; }
    string? Name { get; set; }
    IScriptableZone Zone { get; }
    (float x, float y, float z) Position { get; }

    void Say(string message);
    void SayLocalized(int stringId);
    IAction MoveTo(float x, float y, float z, bool direct);
    void SetAction(string slot, IAction action);
}
