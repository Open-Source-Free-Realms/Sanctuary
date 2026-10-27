using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Zones;

/// <summary>
/// A placed prop or world NPC as the tile editor sees it. Heading is in radians.
/// </summary>
public sealed record TileEntity(ulong Guid, bool IsProp, int DefinitionId, string Label, Vector4 Position, float Heading);

/// <summary>
/// A copy of a prop's or world NPC's saved row, for copying, kits and undoing a delete.
/// Exactly one of <see cref="Fixture"/> and <see cref="WorldNpc"/> is set.
/// </summary>
public sealed record TileSnapshot(DbWorldFixture? Fixture, DbWorldNpc? WorldNpc)
{
    public bool IsProp => Fixture is not null;
}

/// <summary>
/// Bulk editing of the persisted objects in a world zone (props and GM-spawned NPCs) for the tile editor.
/// Every change is saved and streamed to players in view like a single edit.
/// </summary>
public sealed partial class WorldZone
{
    public IReadOnlyList<TileEntity> GetTileEntities(Vector4 center, float radius)
    {
        var radiusSquared = radius * radius;

        return [.. GetAllTileEntities().Where(entity => DistanceSquared2D(entity.Position, center) <= radiusSquared)];
    }

    public bool TryGetTileEntity(ulong guid, [MaybeNullWhen(false)] out TileEntity entity)
    {
        entity = GetAllTileEntities().FirstOrDefault(x => x.Guid == guid);
        return entity is not null;
    }

    private IEnumerable<TileEntity> GetAllTileEntities()
    {
        foreach (var (npc, fixture) in GetWorldFixtures())
        {
            var label = _resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var item) && !string.IsNullOrEmpty(item.Comment)
                ? item.Comment
                : $"Item {fixture.Definition}";

            yield return new TileEntity(npc.Guid, true, fixture.Definition, label, npc.Position, GetWorldFixtureHeading(fixture));
        }

        List<(ulong Guid, DbWorldNpc Row)> worldNpcs;

        lock (_worldNpcLock)
            worldNpcs = [.. _worldNpcs.Select(x => (x.Key, x.Value))];

        foreach (var (guid, row) in worldNpcs)
        {
            if (!TryGetNpc(guid, out var npc))
                continue;

            yield return new TileEntity(guid, false, row.Definition, npc.Name ?? $"NPC {row.Definition}", npc.Position, NpcProperties.GetHeading(npc.SpawnRotation));
        }
    }

    /// <summary>
    /// Moves a prop or world NPC to a position and heading, saves it, and moves it for everyone in view.
    /// </summary>
    public bool TryMoveTileEntity(ulong guid, Vector4 position, float heading)
    {
        position = position with { W = 1.0f };
        var facing = QuaternionExtensions.FromHeading(heading);

        lock (_worldFixtureLock)
        {
            if (_worldFixtures.TryGetValue(guid, out var fixture))
            {
                fixture.PositionX = position.X;
                fixture.PositionY = position.Y;
                fixture.PositionZ = position.Z;

                // The client's angle format (yaw in X); pitch and roll live in the prop's properties.
                fixture.RotationX = heading;
                fixture.RotationY = 0f;
                fixture.RotationZ = 0f;
                fixture.RotationW = 0f;

                using (var dbContext = _dbContextFactory.CreateDbContext())
                {
                    dbContext.WorldFixtures.Update(fixture);
                    dbContext.SaveChanges();
                }

                if (TryGetNpc(guid, out var prop))
                    Place(prop, position, facing, heading);

                return true;
            }
        }

        if (!IsWorldNpc(guid) || !TryGetNpc(guid, out var npc))
            return false;

        Place(npc, position, facing, heading);

        return TrySaveWorldNpc(npc);
    }

    private static void Place(Npc npc, Vector4 position, Quaternion facing, float heading)
    {
        npc.SpawnPosition = position;
        npc.SpawnRotation = facing;
        npc.Tilt = npc.Tilt is { } tilt ? tilt with { X = heading } : null;
        npc.UpdatePosition(position, facing);
    }

    public bool TrySnapshotTileEntity(ulong guid, [MaybeNullWhen(false)] out TileSnapshot snapshot)
    {
        snapshot = null;

        lock (_worldFixtureLock)
        {
            if (_worldFixtures.TryGetValue(guid, out var fixture))
            {
                snapshot = new TileSnapshot(CopyFixture(fixture), null);
                return true;
            }
        }

        lock (_worldNpcLock)
        {
            if (_worldNpcs.TryGetValue(guid, out var row))
            {
                snapshot = new TileSnapshot(null, CopyWorldNpc(row));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a new prop or world NPC from a snapshot at a position and heading. Returns its guid, or null
    /// when it couldn't be spawned (or was saved but its event is off, see <paramref name="spawned"/>).
    /// </summary>
    public ulong? TryCreateFromSnapshot(TileSnapshot snapshot, Vector4 position, float heading, ulong? createdBy, out bool spawned)
    {
        spawned = false;

        if (snapshot.Fixture is { } source)
        {
            lock (_worldFixtureLock)
            {
                var fixture = CopyFixture(source);
                fixture.Id = 0;
                fixture.Zone = DefinitionId;
                fixture.PositionX = position.X;
                fixture.PositionY = position.Y;
                fixture.PositionZ = position.Z;
                fixture.RotationX = heading;
                fixture.RotationY = 0f;
                fixture.RotationZ = 0f;
                fixture.RotationW = 0f;
                fixture.CreatedBy = createdBy;
                fixture.Created = DateTimeOffset.UtcNow;

                using var dbContext = _dbContextFactory.CreateDbContext();

                dbContext.WorldFixtures.Add(fixture);
                dbContext.SaveChanges();

                if (!TrySpawnWorldFixture(fixture, out var prop))
                    return null;

                spawned = true;
                return prop.Guid;
            }
        }

        if (snapshot.WorldNpc is not { } sourceNpc)
            return null;

        lock (_worldNpcLock)
        {
            var row = CopyWorldNpc(sourceNpc);
            var facing = QuaternionExtensions.FromHeading(heading);

            row.Id = 0;
            row.Zone = DefinitionId;
            row.PositionX = position.X;
            row.PositionY = position.Y;
            row.PositionZ = position.Z;
            row.RotationX = facing.X;
            row.RotationY = facing.Y;
            row.RotationZ = facing.Z;
            row.RotationW = facing.W;
            row.CreatedBy = createdBy;
            row.Created = DateTimeOffset.UtcNow;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Add(row);
            dbContext.SaveChanges();

            // Saved either way; an inactive event's NPC spawns when the event is turned on.
            if (row.EventKey is not null && !_worldEventManager.IsActive(row.EventKey))
                return null;

            if (!TrySpawnWorldNpc(row, out var npc))
                return null;

            spawned = true;
            return npc.Guid;
        }
    }

    public bool TryDeleteTileEntity(ulong guid)
    {
        if (TryRemoveWorldFixture(guid, out _))
            return true;

        return IsWorldNpc(guid) && TryGetNpc(guid, out var npc) && TryDeleteWorldNpc(npc);
    }

    private static DbWorldFixture CopyFixture(DbWorldFixture fixture) => new()
    {
        Id = fixture.Id,
        Zone = fixture.Zone,
        Tint = fixture.Tint,
        Definition = fixture.Definition,
        PositionX = fixture.PositionX,
        PositionY = fixture.PositionY,
        PositionZ = fixture.PositionZ,
        RotationX = fixture.RotationX,
        RotationY = fixture.RotationY,
        RotationZ = fixture.RotationZ,
        RotationW = fixture.RotationW,
        Scale = fixture.Scale,
        Collidable = fixture.Collidable,
        Script = fixture.Script,
        Properties = fixture.Properties,
        CreatedBy = fixture.CreatedBy,
        Created = fixture.Created
    };

    private static DbWorldNpc CopyWorldNpc(DbWorldNpc row) => new()
    {
        Id = row.Id,
        Zone = row.Zone,
        Definition = row.Definition,
        Name = row.Name,
        NameId = row.NameId,
        ModelId = row.ModelId,
        Scale = row.Scale,
        Animation = row.Animation,
        Disposition = row.Disposition,
        HideNamePlate = row.HideNamePlate,
        Behavior = row.Behavior,
        WanderRadius = row.WanderRadius,
        Speed = row.Speed,
        PositionX = row.PositionX,
        PositionY = row.PositionY,
        PositionZ = row.PositionZ,
        RotationX = row.RotationX,
        RotationY = row.RotationY,
        RotationZ = row.RotationZ,
        RotationW = row.RotationW,
        EventKey = row.EventKey,
        Collidable = row.Collidable,
        Script = row.Script,
        Conversation = row.Conversation,
        Properties = row.Properties,
        CreatedBy = row.CreatedBy,
        Created = row.Created
    };

    private static float DistanceSquared2D(Vector4 a, Vector4 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }
}
