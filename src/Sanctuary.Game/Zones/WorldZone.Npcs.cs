using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Conversations;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Zones;

/// <summary>
/// NPCs spawned into a world zone by GMs. Every edit is written back, and they respawn when the zone starts.
/// Event-flagged NPCs only spawn while their event is active (see <see cref="IWorldEventManager"/>).
/// </summary>
public sealed partial class WorldZone
{
    private const float DefaultWanderRadius = 10f;

    private readonly IWorldEventManager _worldEventManager;

    private readonly object _worldNpcLock = new();

    /// <summary>
    /// Spawned world NPCs by NPC guid.
    /// </summary>
    private readonly Dictionary<ulong, DbWorldNpc> _worldNpcs = new();

    private bool _worldNpcsLoaded;

    public bool IsWorldNpc(ulong npcGuid)
    {
        lock (_worldNpcLock)
            return _worldNpcs.ContainsKey(npcGuid);
    }

    public string? GetWorldNpcEvent(ulong npcGuid)
    {
        lock (_worldNpcLock)
            return _worldNpcs.TryGetValue(npcGuid, out var worldNpc) ? worldNpc.EventKey : null;
    }

    private void LoadWorldNpcs()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var worldNpcs = dbContext.WorldNpcs
            .AsNoTracking()
            .Where(x => x.Zone == DefinitionId)
            .ToList();

        var spawned = 0;

        foreach (var worldNpc in worldNpcs)
        {
            if (worldNpc.EventKey is not null && !_worldEventManager.IsActive(worldNpc.EventKey))
                continue;

            if (TrySpawnWorldNpc(worldNpc, out _))
                spawned++;
            else
                Logger.LogWarning("Failed to spawn world NPC {id} (definition {definition}).", worldNpc.Id, worldNpc.Definition);
        }

        if (spawned > 0)
            Logger.LogInformation("Spawned {count} world NPC(s).", spawned);
    }

    private bool TrySpawnWorldNpc(DbWorldNpc worldNpc, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = null;

        if (!_resourceManager.Npcs.TryGetValue(worldNpc.Definition, out var definition)
            || !TryCreateNpc(null, definition, out npc))
            return false;

        npc.Name = worldNpc.Name;
        npc.NameId = worldNpc.NameId;
        npc.ModelId = worldNpc.ModelId;
        npc.Scale = worldNpc.Scale;
        npc.Animation = worldNpc.Animation;
        npc.Disposition = worldNpc.Disposition;
        npc.HideNamePlate = worldNpc.HideNamePlate;
        npc.Speed = worldNpc.Speed;

        if (!worldNpc.Collidable)
            npc.SetCollidable(false);

        // Clickable for everyone, so players can target it and referees get the edit menu.
        npc.HasCursor = true;
        npc.CursorId = InteractionMenuHelper.RefereeCursorId;

        var position = new Vector4(worldNpc.PositionX, worldNpc.PositionY, worldNpc.PositionZ, 1.0f);
        var rotation = new Quaternion(worldNpc.RotationX, worldNpc.RotationY, worldNpc.RotationZ, worldNpc.RotationW);

        npc.SpawnPosition = position;
        npc.SpawnRotation = rotation;

        NpcProperties.Apply(npc, worldNpc.Properties);

        npc.UpdatePosition(position, rotation);

        // Follow targets a player, so only Wander survives a respawn.
        if ((NpcBehavior)worldNpc.Behavior == NpcBehavior.Wander)
            npc.SetBehavior(NpcBehavior.Wander, wanderRadius: worldNpc.WanderRadius);

        _worldNpcs.Add(npc.Guid, worldNpc);

        NpcContent.SetConversation(npc, Conversation.FromJson(worldNpc.Conversation));
        AttachSavedScript(npc, worldNpc.Script);

        return true;
    }

    /// <summary>
    /// Compiles a saved script in the background; a broken one is logged and left detached.
    /// </summary>
    private void AttachSavedScript(Npc npc, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        _ = AttachSavedScriptAsync(npc, source);
    }

    private async Task AttachSavedScriptAsync(Npc npc, string source)
    {
        var error = await NpcContent.SetScriptAsync(npc, source);

        if (error is not null)
            Logger.LogWarning("Saved script on {NpcGuid} failed: {Error}", npc.Guid, error);
    }

    /// <summary>
    /// The script saved for a world NPC or prop, even if it currently fails to compile.
    /// </summary>
    public string? GetSavedScript(ulong npcGuid)
    {
        lock (_worldNpcLock)
        {
            if (_worldNpcs.TryGetValue(npcGuid, out var worldNpc))
                return worldNpc.Script;
        }

        return TryGetWorldFixture(npcGuid, out var fixture) ? fixture.Script : null;
    }

    /// <summary>
    /// Saves a script (null removes it) for a world NPC or prop. Compile it first; this only persists.
    /// </summary>
    public bool TrySaveScript(Npc npc, string? source)
    {
        lock (_worldNpcLock)
        {
            if (_worldNpcs.TryGetValue(npc.Guid, out var worldNpc))
            {
                worldNpc.Script = source;

                using var dbContext = _dbContextFactory.CreateDbContext();

                dbContext.WorldNpcs.Update(worldNpc);
                dbContext.SaveChanges();

                return true;
            }
        }

        return TrySaveWorldFixtureScript(npc.Guid, source);
    }

    /// <summary>
    /// Saves a world NPC's conversation (null removes it) and attaches it.
    /// </summary>
    public bool TrySaveConversation(Npc npc, Conversation? conversation)
    {
        lock (_worldNpcLock)
        {
            if (!_worldNpcs.TryGetValue(npc.Guid, out var worldNpc))
                return false;

            worldNpc.Conversation = conversation?.ToJson();

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Update(worldNpc);
            dbContext.SaveChanges();
        }

        NpcContent.SetConversation(npc, conversation);

        return true;
    }

    public bool TryCreateWorldNpc(Player player, NpcDefinition definition, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = null;

        lock (_worldNpcLock)
        {
            // Unregistered; only read for the entity defaults.
            var defaults = new Npc(this);

            var scale = definition.Scale
                ?? (_resourceManager.Models.TryGetValue(definition.ModelId, out var model) && model.Scale != 0f ? model.Scale : 1f);

            var worldNpc = new DbWorldNpc
            {
                Zone = DefinitionId,
                Definition = definition.Id,
                Name = definition.Name,
                NameId = definition.NameId,
                ModelId = definition.ModelId,
                Scale = scale,
                Animation = defaults.Animation,
                Disposition = defaults.Disposition,
                HideNamePlate = defaults.HideNamePlate,
                Behavior = (int)NpcBehavior.Stay,
                WanderRadius = DefaultWanderRadius,
                Speed = defaults.Speed,
                PositionX = player.Position.X,
                PositionY = player.Position.Y,
                PositionZ = player.Position.Z,
                RotationX = player.Rotation.X,
                RotationY = player.Rotation.Y,
                RotationZ = player.Rotation.Z,
                RotationW = player.Rotation.W,
                CreatedBy = GuidHelper.GetPlayerId(player.Guid)
            };

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Add(worldNpc);

            if (dbContext.SaveChanges() <= 0)
                return false;

            if (TrySpawnWorldNpc(worldNpc, out npc))
                return true;

            dbContext.WorldNpcs.Remove(worldNpc);
            dbContext.SaveChanges();

            return false;
        }
    }

    /// <summary>
    /// Writes the NPC's current state back to its row. Does nothing for NPCs that are not world NPCs.
    /// </summary>
    public bool TrySaveWorldNpc(Npc npc)
    {
        lock (_worldNpcLock)
        {
            if (!_worldNpcs.TryGetValue(npc.Guid, out var worldNpc))
                return false;

            worldNpc.Name = npc.Name;
            worldNpc.NameId = npc.NameId;
            worldNpc.ModelId = npc.ModelId;
            worldNpc.Scale = npc.Scale;
            worldNpc.Animation = npc.Animation;
            worldNpc.Disposition = npc.Disposition;
            worldNpc.HideNamePlate = npc.HideNamePlate;
            worldNpc.Behavior = (int)(npc.Behavior == NpcBehavior.Follow ? NpcBehavior.Stay : npc.Behavior);
            worldNpc.WanderRadius = npc.WanderRadius;
            worldNpc.Speed = npc.Speed;
            worldNpc.Collidable = npc.Collidable;
            worldNpc.Properties = NpcProperties.Capture(npc, includeBasics: false);

            worldNpc.PositionX = npc.SpawnPosition.X;
            worldNpc.PositionY = npc.SpawnPosition.Y;
            worldNpc.PositionZ = npc.SpawnPosition.Z;
            worldNpc.RotationX = npc.SpawnRotation.X;
            worldNpc.RotationY = npc.SpawnRotation.Y;
            worldNpc.RotationZ = npc.SpawnRotation.Z;
            worldNpc.RotationW = npc.SpawnRotation.W;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Update(worldNpc);
            dbContext.SaveChanges();

            return true;
        }
    }

    /// <summary>
    /// Flags a world NPC for an event (or clears it with null). Despawns it if that event is inactive.
    /// </summary>
    public bool TrySetWorldNpcEvent(Npc npc, string? eventKey)
    {
        lock (_worldNpcLock)
        {
            if (!_worldNpcs.TryGetValue(npc.Guid, out var worldNpc))
                return false;

            worldNpc.EventKey = eventKey;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Update(worldNpc);
            dbContext.SaveChanges();

            if (eventKey is not null && !_worldEventManager.IsActive(eventKey))
            {
                _worldNpcs.Remove(npc.Guid);
                npc.Dispose();
            }

            return true;
        }
    }

    /// <summary>
    /// Deletes a world NPC for good. Returns false for NPCs that are not world NPCs.
    /// </summary>
    public bool TryDeleteWorldNpc(Npc npc)
    {
        lock (_worldNpcLock)
        {
            if (!_worldNpcs.Remove(npc.Guid, out var worldNpc))
                return false;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldNpcs.Remove(worldNpc);
            dbContext.SaveChanges();

            npc.Dispose();

            return true;
        }
    }

    public void OnWorldEventChanged(string eventKey, bool active)
    {
        lock (_worldNpcLock)
        {
            if (!_worldNpcsLoaded)
                return;

            if (!active)
            {
                foreach (var (npcGuid, worldNpc) in _worldNpcs.ToList())
                {
                    if (!eventKey.Equals(worldNpc.EventKey, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    _worldNpcs.Remove(npcGuid);

                    if (TryGetNpc(npcGuid, out var npc))
                        npc.Dispose();
                }

                return;
            }

            var spawnedIds = _worldNpcs.Values.Select(x => x.Id).ToHashSet();

            using var dbContext = _dbContextFactory.CreateDbContext();

            var eventNpcs = dbContext.WorldNpcs
                .AsNoTracking()
                .Where(x => x.Zone == DefinitionId && x.EventKey == eventKey)
                .ToList();

            foreach (var worldNpc in eventNpcs.Where(x => !spawnedIds.Contains(x.Id)))
            {
                if (!TrySpawnWorldNpc(worldNpc, out _))
                    Logger.LogWarning("Failed to spawn world NPC {id} for event {event}.", worldNpc.Id, eventKey);
            }
        }
    }
}
