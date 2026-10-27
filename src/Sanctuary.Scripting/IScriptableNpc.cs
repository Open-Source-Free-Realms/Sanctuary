using System.Numerics;

namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    ulong Guid { get; }
    string? Name { get; set; }
    IScriptableZone Zone { get; }
    (float x, float y, float z) Position { get; }

    void Say(string message);
    void SayLocalized(int stringId);
    void MoveTo(float x, float y, float z, bool direct);

    /// <summary>
    /// Plays an animation (AnimationTypes.xml id); a looping one becomes the idle.
    /// </summary>
    void Emote(int animationId, bool loop);

    void Face(IScriptablePlayer player);

    /// <summary>
    /// Opens this NPC's conversation for the player, at the given node or its start node.
    /// </summary>
    void StartConversation(IScriptablePlayer player, int? nodeId);
}
