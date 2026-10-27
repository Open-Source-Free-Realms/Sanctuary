using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

public class NpcChatCommand : IRawChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;
    private readonly IWorldEventManager _worldEventManager;

    public string KeyWord => "npc";

    public string Usage => "spawn <definitionId> | despawn [guid] | select [guid] | info | name <text|clear> | anim <id> [loop] | scale <n> | model <id> " +
        "| disposition hostile|neutral|ally | nameplate on|off | behavior stay|follow|wander [radius] | speed <n> | move | face | home | use | say <text> | event <key|none> | collide on|off | " + ScriptCommandHelper.Usage;

    public string Description => "Spawns, despawns and edits NPCs. Edits apply to the NPC you last clicked (or 'select'ed) and show for everyone nearby.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public NpcChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager, IWorldEventManager worldEventManager)
    {
        _chatCommandManager = chatCommandManager;
        _resourceManager = resourceManager;
        _worldEventManager = worldEventManager;
    }

    private void LogAction(Player invoker, string action, string? targetName = null, string? detail = null)
    {
        _chatCommandManager.LogAction(this, invoker, action, targetName, detail);
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length < 1)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "spawn":
                return Spawn(invoker, args[1..]);
            case "despawn":
                return Despawn(invoker, args[1..]);
            case "select":
                return Select(invoker, args[1..]);
            case "script":
                if (invoker.SelectedNpcGuid is not { } scriptGuid || !invoker.Zone.TryGetNpc(scriptGuid, out var scriptNpc) || !IsDespawnable(scriptNpc))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Click an NPC or use !{KeyWord} select first.");
                    return true;
                }

                return ScriptCommandHelper.Handle(this, _chatCommandManager, invoker, scriptNpc, "npc", args[1..], ChatCommandText.Remainder(rawArgs, 1));
            case "info":
            case "name":
            case "anim":
            case "scale":
            case "model":
            case "disposition":
            case "nameplate":
            case "behavior":
            case "speed":
            case "move":
            case "say":
            case "event":
            case "face":
            case "home":
            case "use":
            case "collide":
                return Edit(invoker, args[0].ToLowerInvariant(), args[1..]);
            default:
                return false;
        }
    }

    private bool Spawn(Player invoker, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var definitionId))
            return false;

        if (!_resourceManager.Npcs.TryGetValue(definitionId, out var definition))
        {
            ChatHelper.SendSystemMessage(invoker, $"No NPC definition with id {definitionId} was found.");
            return true;
        }

        Npc? npc;

        // World zones persist GM-spawned NPCs; anywhere else (houses, combat zones) they last until the zone closes.
        var persisted = invoker.Zone is WorldZone;

        if (invoker.Zone is WorldZone worldZone)
        {
            if (!worldZone.TryCreateWorldNpc(invoker, definition, out npc))
            {
                ChatHelper.SendSystemMessage(invoker, "The NPC could not be spawned.");
                return true;
            }
        }
        else
        {
            if (!invoker.Zone.TryCreateNpc(null, definition, out npc))
            {
                ChatHelper.SendSystemMessage(invoker, "The NPC could not be spawned.");
                return true;
            }

            // Clickable for everyone, so players can target it and referees get the edit menu.
            npc.HasCursor = true;
            npc.CursorId = InteractionMenuHelper.RefereeCursorId;
            npc.SpawnPosition = invoker.Position;
            npc.SpawnRotation = invoker.Rotation;

            npc.UpdatePosition(invoker.Position, invoker.Rotation);
        }

        RefereeEditorHelper.Select(invoker, npc);

        LogAction(invoker, "Spawn NPC", $"#{definitionId}", $"guid={npc.Guid}, position={npc.Position}, persisted={persisted}");
        ChatHelper.SendSystemMessage(invoker, $"Spawned NPC {npc.Name} (def #{definitionId} guid {npc.Guid}){(persisted ? " and saved it" : "; it is not saved outside world zones")}.");
        return true;
    }

    private bool Despawn(Player invoker, string[] args)
    {
        if (args.Length > 1)
            return false;

        Npc? target;

        if (args.Length == 1)
        {
            if (!ulong.TryParse(args[0], out var guid))
                return false;

            if (!invoker.Zone.TryGetNpc(guid, out target) || !IsDespawnable(target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No NPC with guid {guid} was found in your zone.");
                return true;
            }
        }
        else
        {
            target = FindNearestNpc(invoker);

            if (target is null)
            {
                ChatHelper.SendSystemMessage(invoker, "There are no NPCs in your zone to despawn.");
                return true;
            }
        }

        var removedGuid = target.Guid;
        var removedName = target.Name;
        WorldNpcHelper.Remove(target);

        if (invoker.SelectedNpcGuid == removedGuid)
            RefereeEditorHelper.Select(invoker, null);

        LogAction(invoker, "Despawn NPC", $"guid={removedGuid}");
        ChatHelper.SendSystemMessage(invoker, $"Despawned NPC {removedName} (guid {removedGuid}).");
        return true;
    }

    private bool Select(Player invoker, string[] args)
    {
        if (args.Length > 1)
            return false;

        Npc? target;

        if (args.Length == 1)
        {
            if (!ulong.TryParse(args[0], out var guid))
                return false;

            if (!invoker.Zone.TryGetNpc(guid, out target) || !IsDespawnable(target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No NPC with guid {guid} was found in your zone.");
                return true;
            }
        }
        else
        {
            target = FindNearestNpc(invoker);

            if (target is null)
            {
                ChatHelper.SendSystemMessage(invoker, "There are no NPCs in your zone.");
                return true;
            }
        }

        RefereeEditorHelper.Select(invoker, target);
        ChatHelper.SendSystemMessage(invoker, $"Selected NPC {target.Name} (guid {target.Guid}).");
        return true;
    }

    private bool Edit(Player invoker, string field, string[] args)
    {
        if (invoker.SelectedNpcGuid is not { } guid || !invoker.Zone.TryGetNpc(guid, out var npc) || !IsDespawnable(npc))
        {
            ChatHelper.SendSystemMessage(invoker, $"Click an NPC or use !{KeyWord} select first.");
            return true;
        }

        var value = string.Join(' ', args);

        switch (field)
        {
            case "info":
                ChatHelper.SendSystemMessage(invoker,
                    $"{npc.Name ?? $"NameId {npc.NameId}"} (guid {npc.Guid}): Model {npc.ModelId}, Scale {npc.Scale}, Animation {npc.Animation}, " +
                    $"Disposition {npc.Disposition}, Behavior {npc.Behavior}, Speed {npc.Speed}, Nameplate {(npc.HideNamePlate ? "hidden" : "shown")}, " +
                    $"Position ({npc.Position.X:F1}, {npc.Position.Y:F1}, {npc.Position.Z:F1})");
                return true;

            case "name":
                if (args.Length == 0)
                    return false;

                npc.Name = value.Equals("clear", StringComparison.OrdinalIgnoreCase) ? null : value;

                // The client prefers the localized NameId, so drop it for a custom name.
                if (npc.Name is not null)
                    npc.NameId = 0;

                npc.RefreshForVisiblePlayers();
                break;

            case "anim":
                if (args.Length is < 1 or > 2 || !int.TryParse(args[0], out var animationId))
                    return false;

                npc.PlayAnimation(animationId, args.Length == 2 && args[1].Equals("loop", StringComparison.OrdinalIgnoreCase));
                break;

            case "scale":
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || scale <= 0f || scale > 20f)
                {
                    ChatHelper.SendSystemMessage(invoker, "Scale must be above 0 and at most 20.");
                    return true;
                }

                npc.Scale = scale;
                npc.RefreshForVisiblePlayers();
                break;

            case "model":
                if (!int.TryParse(value, out var modelId))
                    return false;

                if (!_resourceManager.Models.ContainsKey(modelId))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Unknown model {modelId}.");
                    return true;
                }

                npc.ModelId = modelId;
                npc.RefreshForVisiblePlayers();
                break;

            case "disposition":
                int? disposition = value.ToLowerInvariant() switch
                {
                    "hostile" => 0,
                    "neutral" => 1,
                    "ally" => 2,
                    _ => null
                };

                if (disposition is null)
                    return false;

                npc.Disposition = disposition.Value;
                npc.RefreshForVisiblePlayers();
                break;

            case "nameplate":
                if (!value.Equals("on", StringComparison.OrdinalIgnoreCase) && !value.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return false;

                npc.HideNamePlate = value.Equals("off", StringComparison.OrdinalIgnoreCase);
                npc.RefreshForVisiblePlayers();
                break;

            case "behavior":
                if (args.Length is < 1 or > 2 || !Enum.TryParse<NpcBehavior>(args[0], true, out var behavior) || !Enum.IsDefined(behavior))
                    return false;

                var radius = 10f;

                if (args.Length == 2 && (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) || radius <= 0f))
                    return false;

                npc.SetBehavior(behavior, invoker.Guid, radius);
                break;

            case "speed":
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || speed <= 0f || speed > 50f)
                {
                    ChatHelper.SendSystemMessage(invoker, "Speed must be above 0 and at most 50.");
                    return true;
                }

                npc.Speed = speed;
                npc.RefreshForVisiblePlayers();
                break;

            case "move":
                npc.SetBehavior(NpcBehavior.Stay);
                npc.UpdatePosition(invoker.Position, invoker.Rotation);

                npc.SpawnPosition = npc.Position;
                npc.SpawnRotation = npc.Rotation;
                break;

            case "face":
                if (!npc.FaceTowards(invoker.Position))
                    return true;

                npc.SpawnRotation = npc.Rotation;
                break;

            case "collide":
                if (!value.Equals("on", StringComparison.OrdinalIgnoreCase) && !value.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return false;

                npc.SetCollidable(value.Equals("on", StringComparison.OrdinalIgnoreCase));
                break;

            case "home":
                if (npc.SpawnPosition == default)
                {
                    ChatHelper.SendSystemMessage(invoker, "This NPC has no spawn position.");
                    return true;
                }

                npc.SetBehavior(NpcBehavior.Stay);
                npc.MoveTo(new Vector3(npc.SpawnPosition.X, npc.SpawnPosition.Y, npc.SpawnPosition.Z));
                break;

            case "use":
                if (npc.InteractAction is null)
                {
                    ChatHelper.SendSystemMessage(invoker, "This NPC has no default interaction.");
                    return true;
                }

                npc.InteractAction(invoker);
                return true;

            case "event":
                if (args.Length != 1)
                    return false;

                if (npc.Zone is not WorldZone eventZone || !eventZone.IsWorldNpc(npc.Guid))
                {
                    ChatHelper.SendSystemMessage(invoker, "Only NPCs saved in a world zone can be flagged for an event.");
                    return true;
                }

                var eventKey = value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value.ToLowerInvariant();

                if (eventKey is { Length: > 32 })
                {
                    ChatHelper.SendSystemMessage(invoker, "Event keys are at most 32 characters.");
                    return true;
                }

                eventZone.TrySetWorldNpcEvent(npc, eventKey);
                RefereeEditorHelper.Refresh(invoker);

                LogAction(invoker, "Edit NPC event", $"guid={npc.Guid}", $"event={eventKey ?? "none"}");
                ChatHelper.SendSystemMessage(invoker, eventKey is null
                    ? "Event flag cleared; the NPC always spawns."
                    : $"Flagged for event \"{eventKey}\". It only spawns while !event on {eventKey} is set" +
                      (_worldEventManager.IsActive(eventKey) ? "." : ", so it was despawned for now."));
                return true;

            case "say":
                if (args.Length == 0)
                    return false;

                npc.Say(value);
                break;
        }

        if (field != "info")
        {
            var saved = field != "say" && WorldNpcHelper.Save(npc);

            RefereeEditorHelper.Refresh(invoker);

            LogAction(invoker, $"Edit NPC {field}", $"guid={npc.Guid}", value.Length > 0 ? $"value={value}" : null);
            ChatHelper.SendSystemMessage(invoker, $"Updated {field} on {npc.Name ?? $"NameId {npc.NameId}"} (guid {npc.Guid}){(saved ? " and saved it" : string.Empty)}.");
        }

        return true;
    }

    private static Npc? FindNearestNpc(Player invoker)
    {
        return invoker.Zone.Npcs
            .Where(IsDespawnable)
            .MinBy(npc => Vector4.DistanceSquared(invoker.Position, npc.Position));
    }

    private static bool IsDespawnable(Npc npc)
    {
        // Mounts are owned by players and collection nodes are managed by their pools,
        // so neither should be removed directly through this command.
        return npc is not Mount and not CollectionNode;
    }
}
