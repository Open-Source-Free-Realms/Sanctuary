using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

public interface IEntity : IEquatable<IEntity>, IDisposable
{
    ulong Guid { get; init; }

    Vector4 Position { get; }
    Quaternion Rotation { get; }

    bool Visible { get; set; }

    #region Zone

    IZone Zone { get; }
    ZoneTile ZoneTile { get; }

    #endregion

    #region Visibility

    ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; }
    ConcurrentDictionary<ulong, Player> VisiblePlayers { get; }

    #endregion

    #region Update

    void UpdatePosition(Vector4 position, Quaternion rotation);
    void TeleportToZone(IZone zone, Vector4 position, Quaternion rotation);

    void UpdateEveryTick();
    void UpdateEverySecond();

    #endregion

    #region Events

    void OnInteract(IEntity entity);

    void OnAddVisibleNpcs(IEnumerable<Npc> npcs);
    void OnAddVisiblePlayers(IEnumerable<Player> players);
    void OnRemoveVisibleNpcs(IEnumerable<Npc> npcs);
    void OnRemoveVisiblePlayers(IEnumerable<Player> players);

    #endregion
}