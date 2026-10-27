using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// The tile editor: bulk move, rotate, copy and delete of a world zone's props and GM-spawned NPCs,
/// with undo and saved kits. Each change is saved and streamed to players like a single edit. The GM
/// panel shows the selection from tilesel/tileundo/tilekits lines.
/// </summary>
public class TileChatCommand : IRawChatCommand
{
    private const int MaxSelection = 50;
    private const float MaxRadius = 100f;
    private const float DefaultCopyOffset = 2f;

    private readonly ITileEditor _tileEditor;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "tile";
    public string Usage => "select radius <r> | select add|remove [guid] | select clear | list | move <dx> <dy> <dz> | here | rotate <degrees> " +
        "| copy [dx] [dz] | delete | undo | kit save <name> [note] | kit place <name> | kit list | kit delete <name>";
    public string Description => $"Edits groups of props and saved NPCs in a world zone (up to {MaxSelection} at once), with undo and reusable kits.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public TileChatCommand(ITileEditor tileEditor, IChatCommandManager chatCommandManager)
    {
        _tileEditor = tileEditor;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        if (invoker.Zone is not WorldZone zone)
        {
            ChatHelper.SendSystemMessage(invoker, "The tile editor works in world zones.");
            return true;
        }

        var state = _tileEditor.GetState(invoker);

        // A selection doesn't carry over to another zone.
        if (!ReferenceEquals(state.SelectionZone, zone))
        {
            state.Selection.Clear();
            state.SelectionZone = zone;
        }

        var action = args[0].ToLowerInvariant();

        bool? handled = action switch
        {
            "select" => Select(invoker, zone, state, args),
            "list" => true,
            "move" => Move(invoker, zone, state, args),
            "here" => MoveHere(invoker, zone, state),
            "rotate" => Rotate(invoker, zone, state, args),
            "copy" => Copy(invoker, zone, state, args),
            "delete" => Delete(invoker, zone, state),
            "undo" => Undo(invoker, zone, state),
            "kit" => Kit(invoker, zone, state, args, rawArgs),
            _ => null
        };

        if (handled is null)
            return false;

        if (action is not ("select" or "list" or "kit"))
            _chatCommandManager.LogAction(this, invoker, action, null, $"Zone: {zone.Name}, Count: {state.Selection.Count}, Args: {rawArgs}");

        SendState(invoker, zone, state);

        return handled.Value;
    }

    #region Selection

    private static bool Select(Player invoker, WorldZone zone, TileEditorState state, string[] args)
    {
        if (args.Length < 2)
            return false;

        switch (args[1].ToLowerInvariant())
        {
            case "radius":
                if (args.Length != 3 || !TryParseFloat(args[2], out var radius) || radius <= 0f || radius > MaxRadius)
                {
                    ChatHelper.SendSystemMessage(invoker, $"Radius must be above 0 and at most {MaxRadius}.");
                    return true;
                }

                var nearby = zone.GetTileEntities(invoker.Position, radius)
                    .OrderBy(x => Vector4.DistanceSquared(x.Position, invoker.Position))
                    .Take(MaxSelection)
                    .Select(x => x.Guid);

                state.Selection.Clear();
                state.Selection.AddRange(nearby);
                return true;

            case "add":
            case "remove":
                ulong guid;

                if (args.Length == 3)
                {
                    if (!ulong.TryParse(args[2], out guid))
                        return false;
                }
                else if (invoker.SelectedNpcGuid is { } selected)
                {
                    guid = selected;
                }
                else
                {
                    ChatHelper.SendSystemMessage(invoker, "Click a prop or NPC in god mode, or give its guid.");
                    return true;
                }

                if (args[1].Equals("remove", StringComparison.OrdinalIgnoreCase))
                {
                    state.Selection.Remove(guid);
                    return true;
                }

                if (!zone.TryGetTileEntity(guid, out _))
                {
                    ChatHelper.SendSystemMessage(invoker, "That isn't a placed prop or saved NPC in this zone.");
                    return true;
                }

                if (!state.Selection.Contains(guid) && state.Selection.Count < MaxSelection)
                    state.Selection.Add(guid);
                return true;

            case "clear":
                state.Selection.Clear();
                return true;

            default:
                return false;
        }
    }

    private static List<TileEntity> SelectedEntities(WorldZone zone, TileEditorState state)
    {
        var entities = new List<TileEntity>();

        // Drop anything removed or despawned since it was selected.
        foreach (var guid in state.Selection.ToList())
        {
            if (zone.TryGetTileEntity(guid, out var entity))
                entities.Add(entity);
            else
                state.Selection.Remove(guid);
        }

        return entities;
    }

    private static bool RequireSelection(Player invoker, List<TileEntity> entities)
    {
        if (entities.Count > 0)
            return true;

        ChatHelper.SendSystemMessage(invoker, "Select something first: !tile select radius <r>, or click and !tile select add.");
        return false;
    }

    #endregion

    #region Edits

    private static bool Move(Player invoker, WorldZone zone, TileEditorState state, string[] args)
    {
        if (args.Length != 4 || !TryParseFloat(args[1], out var dx) || !TryParseFloat(args[2], out var dy) || !TryParseFloat(args[3], out var dz))
            return false;

        return Transform(invoker, zone, state, $"move {dx} {dy} {dz}", entity =>
            (entity.Position + new Vector4(dx, dy, dz, 0f), entity.Heading));
    }

    private static bool MoveHere(Player invoker, WorldZone zone, TileEditorState state)
    {
        var entities = SelectedEntities(zone, state);

        if (!RequireSelection(invoker, entities))
            return true;

        var offset = invoker.Position - TileEditor.Centroid(entities);

        return Transform(invoker, zone, state, "move here", entity =>
            (entity.Position + offset with { W = 0f }, entity.Heading));
    }

    private static bool Rotate(Player invoker, WorldZone zone, TileEditorState state, string[] args)
    {
        if (args.Length != 2 || !TryParseFloat(args[1], out var degrees))
            return false;

        var entities = SelectedEntities(zone, state);

        if (!RequireSelection(invoker, entities))
            return true;

        var angle = degrees * MathF.PI / 180f;
        var center = TileEditor.Centroid(entities);

        return Transform(invoker, zone, state, $"rotate {degrees}", entity =>
        {
            var (dx, dz) = TileEditor.Rotate(entity.Position.X - center.X, entity.Position.Z - center.Z, angle);

            return (new Vector4(center.X + dx, entity.Position.Y, center.Z + dz, 1f), entity.Heading + angle);
        });
    }

    /// <summary>
    /// Moves every selected entity and records how to put them back.
    /// </summary>
    private static bool Transform(Player invoker, WorldZone zone, TileEditorState state, string label,
        Func<TileEntity, (Vector4 Position, float Heading)> transform)
    {
        var entities = SelectedEntities(zone, state);

        if (!RequireSelection(invoker, entities))
            return true;

        var previous = entities.Select(x => (x.Guid, x.Position, x.Heading)).ToList();

        foreach (var entity in entities)
        {
            var (position, heading) = transform(entity);
            zone.TryMoveTileEntity(entity.Guid, position, heading);
        }

        state.PushUndo(new TileUndo(label, zone, worldZone =>
        {
            foreach (var (guid, position, heading) in previous)
                worldZone.TryMoveTileEntity(guid, position, heading);
        }));

        return true;
    }

    private static bool Copy(Player invoker, WorldZone zone, TileEditorState state, string[] args)
    {
        var dx = DefaultCopyOffset;
        var dz = 0f;

        if (args.Length > 3 || (args.Length >= 2 && !TryParseFloat(args[1], out dx)) || (args.Length == 3 && !TryParseFloat(args[2], out dz)))
            return false;

        var entities = SelectedEntities(zone, state);

        if (!RequireSelection(invoker, entities))
            return true;

        var created = new List<ulong>();

        foreach (var entity in entities)
        {
            if (!zone.TrySnapshotTileEntity(entity.Guid, out var snapshot))
                continue;

            var guid = zone.TryCreateFromSnapshot(snapshot, entity.Position + new Vector4(dx, 0f, dz, 0f), entity.Heading, invoker.CharacterId, out _);

            if (guid is { } newGuid)
                created.Add(newGuid);
        }

        // The copies become the selection, ready to move.
        state.Selection.Clear();
        state.Selection.AddRange(created);

        state.PushUndo(new TileUndo($"copy {created.Count}", zone, worldZone =>
        {
            foreach (var guid in created)
                worldZone.TryDeleteTileEntity(guid);
        }));

        ChatHelper.SendSystemMessage(invoker, $"Copied {created.Count} of {entities.Count}.");
        return true;
    }

    private static bool Delete(Player invoker, WorldZone zone, TileEditorState state)
    {
        var entities = SelectedEntities(zone, state);

        if (!RequireSelection(invoker, entities))
            return true;

        var deleted = new List<(TileSnapshot Snapshot, Vector4 Position, float Heading)>();

        foreach (var entity in entities)
        {
            if (zone.TrySnapshotTileEntity(entity.Guid, out var snapshot) && zone.TryDeleteTileEntity(entity.Guid))
                deleted.Add((snapshot, entity.Position, entity.Heading));
        }

        state.Selection.Clear();

        state.PushUndo(new TileUndo($"delete {deleted.Count}", zone, worldZone =>
        {
            foreach (var (snapshot, position, heading) in deleted)
                worldZone.TryCreateFromSnapshot(snapshot, position, heading, snapshot.Fixture?.CreatedBy ?? snapshot.WorldNpc?.CreatedBy, out _);
        }));

        ChatHelper.SendSystemMessage(invoker, $"Deleted {deleted.Count}. !tile undo brings them back.");
        return true;
    }

    private static bool Undo(Player invoker, WorldZone zone, TileEditorState state)
    {
        if (state.Undo.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, "Nothing to undo.");
            return true;
        }

        var undo = state.Undo[^1];

        if (!ReferenceEquals(undo.Zone, zone))
        {
            ChatHelper.SendSystemMessage(invoker, $"The last edit ({undo.Label}) was made in another zone.");
            return true;
        }

        state.Undo.RemoveAt(state.Undo.Count - 1);
        undo.Revert(zone);

        ChatHelper.SendSystemMessage(invoker, $"Undid {undo.Label}.");
        return true;
    }

    #endregion

    #region Kits

    private bool Kit(Player invoker, WorldZone zone, TileEditorState state, string[] args, string rawArgs)
    {
        if (args.Length < 2)
            return false;

        var sub = args[1].ToLowerInvariant();

        switch (sub)
        {
            case "list":
                SendKits(invoker);
                return true;

            case "delete":
                if (args.Length != 3)
                    return false;

                ChatHelper.SendSystemMessage(invoker, _tileEditor.DeleteKit(args[2]) ? $"Deleted kit {args[2]}." : $"No kit named {args[2]}.");
                SendKits(invoker);
                return true;

            case "save":
                if (args.Length < 3)
                    return false;

                var entities = SelectedEntities(zone, state);

                if (!RequireSelection(invoker, entities))
                    return true;

                // Stored relative to the selection's centre and the GM's facing, so placing turns it to face the same way.
                var origin = TileEditor.Centroid(entities);
                var facing = NpcProperties.GetHeading(invoker.Rotation);
                var entries = new List<TileKitEntry>();

                foreach (var entity in entities)
                {
                    if (!zone.TrySnapshotTileEntity(entity.Guid, out var snapshot))
                        continue;

                    var (dx, dz) = TileEditor.Rotate(entity.Position.X - origin.X, entity.Position.Z - origin.Z, -facing);

                    entries.Add(new TileKitEntry
                    {
                        Fixture = snapshot.Fixture,
                        WorldNpc = snapshot.WorldNpc,
                        Dx = dx,
                        Dy = entity.Position.Y - origin.Y,
                        Dz = dz,
                        Heading = entity.Heading - facing
                    });
                }

                var note = args.Length > 3 ? PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 3)) : null;

                if (_tileEditor.SaveKit(args[2], entries, note, invoker.CharacterId) is { } error)
                {
                    ChatHelper.SendSystemMessage(invoker, error);
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "KitSave", null, $"Kit: {args[2]}, Count: {entries.Count}");
                ChatHelper.SendSystemMessage(invoker, $"Saved kit {args[2]} ({entries.Count}).");
                SendKits(invoker);
                return true;

            case "place":
                if (args.Length != 3)
                    return false;

                if (!_tileEditor.TryGetKit(args[2], out var kit))
                {
                    ChatHelper.SendSystemMessage(invoker, $"No kit named {args[2]}.");
                    return true;
                }

                var placeFacing = NpcProperties.GetHeading(invoker.Rotation);
                var placed = new List<ulong>();

                foreach (var entry in kit)
                {
                    var (dx, dz) = TileEditor.Rotate(entry.Dx, entry.Dz, placeFacing);
                    var position = new Vector4(invoker.Position.X + dx, invoker.Position.Y + entry.Dy, invoker.Position.Z + dz, 1f);

                    var guid = zone.TryCreateFromSnapshot(new TileSnapshot(entry.Fixture, entry.WorldNpc), position, entry.Heading + placeFacing, invoker.CharacterId, out _);

                    if (guid is { } newGuid)
                        placed.Add(newGuid);
                }

                state.Selection.Clear();
                state.Selection.AddRange(placed);

                state.PushUndo(new TileUndo($"place {args[2]}", zone, worldZone =>
                {
                    foreach (var guid in placed)
                        worldZone.TryDeleteTileEntity(guid);
                }));

                _chatCommandManager.LogAction(this, invoker, "KitPlace", null, $"Kit: {args[2]}, Zone: {zone.Name}, Count: {placed.Count}");
                ChatHelper.SendSystemMessage(invoker, $"Placed kit {args[2]} ({placed.Count} of {kit.Count}).");
                return true;

            default:
                return false;
        }
    }

    private void SendKits(Player invoker)
    {
        ChatHelper.SendGmPanelData(invoker, "tilekits", "begin");

        foreach (var kit in _tileEditor.ListKits())
        {
            var count = _tileEditor.TryGetKit(kit.Name, out var entries) ? entries.Count : 0;
            ChatHelper.SendGmPanelData(invoker, "tilekits", "entry", kit.Name, count, PanelTextHelper.Escape(kit.Note ?? string.Empty));
        }

        ChatHelper.SendGmPanelData(invoker, "tilekits", "end");
    }

    #endregion

    /// <summary>
    /// tilesel: the selection with its centre; tileundo: undo depth and what the next undo reverts.
    /// </summary>
    private static void SendState(Player invoker, WorldZone zone, TileEditorState state)
    {
        var entities = SelectedEntities(zone, state);
        var center = TileEditor.Centroid(entities);

        ChatHelper.SendGmPanelData(invoker, "tilesel", "begin", entities.Count, Format(center.X), Format(center.Y), Format(center.Z));

        foreach (var entity in entities)
        {
            ChatHelper.SendGmPanelData(invoker, "tilesel", "entry", entity.Guid, entity.IsProp ? "prop" : "npc", entity.DefinitionId,
                PanelTextHelper.Escape(entity.Label), Format(entity.Position.X), Format(entity.Position.Y), Format(entity.Position.Z),
                Format(entity.Heading * 180f / MathF.PI));
        }

        ChatHelper.SendGmPanelData(invoker, "tilesel", "end");
        ChatHelper.SendGmPanelData(invoker, "tileundo", state.Undo.Count, PanelTextHelper.Escape(state.Undo.Count > 0 ? state.Undo[^1].Label : string.Empty));
    }

    private static bool TryParseFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
