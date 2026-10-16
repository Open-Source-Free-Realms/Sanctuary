using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptableZone _zone) : IScriptableNpc
{
    public ulong Guid { get => 0; init => throw new NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new NotImplementedException(); }
    public ILogger Logger { get; } = _zone.Logger;
    public ScriptRuntime ScriptRuntime => _zone.ScriptRuntime;
    public IScriptableZone Zone => _zone;
    public (float x, float y, float z) Position => (0, 0, 0);

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new NotImplementedException();
    }

    public void Say(string message)
    {
        throw new NotImplementedException();
    }

    public void SayLocalized(int stringId)
    {
        throw new NotImplementedException();
    }

    public void MoveTo(float x, float y, float z, bool direct)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius)
    {
        throw new NotImplementedException();
    }

    public void PlayAnimation(int animationId)
    {
        throw new NotImplementedException();
    }

    public void SetBaseAnimation(int animationId)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight)
    {
        throw new NotImplementedException();
    }

    public void SetCollidable(bool collidable)
    {
        throw new NotImplementedException();
    }

    public int AddEffectTag(int compositeEffectId)
    {
        throw new NotImplementedException();
    }

    public void RemoveEffectTag(int tagId)
    {
        throw new NotImplementedException();
    }

    public bool IsMoving => false;

    public void SetSpeed(float speed)
    {
        throw new NotImplementedException();
    }

    public void SetHeading(float heading)
    {
        throw new NotImplementedException();
    }

    public void PlayEffect(int compositeEffectId)
    {
        throw new NotImplementedException();
    }

    public void Remove(int compositeEffectId)
    {
        throw new NotImplementedException();
    }

    public bool TryAddScript(string scriptName)
    {
        throw new NotImplementedException();
    }

    public bool TryRemoveScript(string scriptName)
    {
        throw new NotImplementedException();
    }
}