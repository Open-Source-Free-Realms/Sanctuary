using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace Sanctuary.Game.Entities;

public interface IEntity : IDisposable
{
    ulong Guid { get; init; }
    Vector4 Position { get; set; }
    Quaternion Rotation { get; set; }

    bool Visible { get; set; }

    IZone Zone { get; }

    void OnEntityAdd(IEntity entity);
    void OnEntityRemove(IEntity entity);
    ConcurrentDictionary<ulong, IEntity> VisibleEntities { get; }

    void Update();
    void UpdateEverySecond();
}