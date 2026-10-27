using System.Globalization;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Edits the placed prop (world fixture) a referee last clicked in god mode. Used by the GM panel's editor.
/// </summary>
public class PropChatCommand : IRawChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "prop";
    public string Usage => "info | move | remove | scale <n> | tint <n> | " + ScriptCommandHelper.Usage;
    public string Description => "Edits the placed prop you last clicked: move opens the furniture tool, remove deletes it.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public PropChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        if (invoker.Zone is not WorldZone worldZone
            || invoker.SelectedNpcGuid is not { } guid
            || !worldZone.TryGetWorldFixture(guid, out var fixture))
        {
            ChatHelper.SendSystemMessage(invoker, "Click a placed prop in god mode first.");
            return true;
        }

        if (args[0].Equals("script", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!worldZone.TryGetNpc(guid, out var prop))
                return true;

            return ScriptCommandHelper.Handle(this, _chatCommandManager, invoker, prop, "prop", args[1..], ChatCommandText.Remainder(rawArgs, 1));
        }

        if (args.Length > 2)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "info":
                ChatHelper.SendSystemMessage(invoker,
                    $"Prop #{fixture.Id}: item {fixture.Definition}, tint {fixture.Tint}, scale {fixture.Scale}, " +
                    $"position ({fixture.PositionX:F1}, {fixture.PositionY:F1}, {fixture.PositionZ:F1}), guid {guid}.");
                return true;

            case "move":
                if (!worldZone.IsPlacing(invoker) && !worldZone.TryBeginMoving(invoker))
                    return true;

                ChatHelper.SendSystemMessage(invoker, "Furniture tool open: drag props to move them, pick one up to delete it. !place off to stop.");
                return true;

            case "remove":
                if (!worldZone.TryRemoveWorldFixture(guid, out _))
                    return true;

                RefereeEditorHelper.Select(invoker, null);

                _chatCommandManager.LogAction(this, invoker, "Remove", null, $"Prop: {fixture.Id}, Item: {fixture.Definition}");
                ChatHelper.SendSystemMessage(invoker, $"Removed prop #{fixture.Id}.");
                return true;

            case "scale":
                if (args.Length != 2
                    || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                    || scale <= 0f || scale > 20f)
                {
                    ChatHelper.SendSystemMessage(invoker, "Scale must be above 0 and at most 20.");
                    return true;
                }

                return SetAppearance(invoker, worldZone, guid, fixture.Id, scale, fixture.Tint);

            case "tint":
                if (args.Length != 2 || !int.TryParse(args[1], out var tint) || tint < 0)
                    return false;

                return SetAppearance(invoker, worldZone, guid, fixture.Id, fixture.Scale, tint);

            default:
                return false;
        }
    }

    private bool SetAppearance(Player invoker, WorldZone worldZone, ulong guid, ulong fixtureId, float scale, int tint)
    {
        if (!worldZone.TrySetWorldFixtureAppearance(guid, scale, tint))
            return true;

        RefereeEditorHelper.Refresh(invoker);

        _chatCommandManager.LogAction(this, invoker, "Appearance", null, $"Prop: {fixtureId}, Scale: {scale}, Tint: {tint}");
        ChatHelper.SendSystemMessage(invoker, $"Prop #{fixtureId} now scale {scale}, tint {tint}.");

        return true;
    }
}
