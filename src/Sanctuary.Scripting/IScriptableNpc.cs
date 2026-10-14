using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    ulong Guid { get; }
    string? Name { get; set; }
    IScriptableZone Zone { get; }
    (float x, float y, float z) Position { get; }

    bool IsMoving { get; }

    void Say(string message);
    void SayLocalized(int stringId);
    void MoveTo(float x, float y, float z, bool direct);

    void SetHeading(float heading);

    void SetSpeed(float speed);

    IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius);

    void PlayAnimation(int animationId);

    void SetBaseAnimation(int animationId);

    IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight);

    void SetCollidable(bool collidable);

    int AddEffectTag(int compositeEffectId);

    void RemoveEffectTag(int tagId);

    void PlayEffect(int compositeEffectId);

    void Remove(int compositeEffectId);
}
