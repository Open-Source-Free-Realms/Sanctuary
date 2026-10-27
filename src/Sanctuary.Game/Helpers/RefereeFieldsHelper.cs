using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Describes every field the GM panel's editor can change on the selected NPC or prop, so the panel draws one
/// grouped form for both and gains new fields without its own changes. Sent as:
/// <code>
/// fields|begin|&lt;npc|prop&gt;|&lt;guid&gt;|&lt;title&gt;|&lt;subtitle&gt;
/// fields|f|&lt;group&gt;|&lt;label&gt;|&lt;type&gt;|&lt;value&gt;|&lt;command&gt;|&lt;options&gt;
/// fields|end
/// </code>
/// Types: text and num (an input; the command gets the text in place of {v}, or options when it is left empty),
/// toggle (on/off), choice (options "Label=current value=argument;..."), action (a button running the command),
/// icons (the overhead icon picker; value lists the current icons as "type:icon:state;..."), steps (buttons
/// that each send the command with one of the options "a;b;..." in place of {v}).
/// </summary>
public static class RefereeFieldsHelper
{
    private sealed record Field(string Group, string Label, string Type, object? Value, string Command, string Options = "");

    public static void Send(Player player, Npc npc, string kind, string? eventKey)
    {
        var isProp = kind == "prop";
        var fields = isProp ? GetPropFields(npc) : GetNpcFields(npc, eventKey);

        var title = npc.Name ?? (isProp ? "Prop" : "Unnamed NPC");
        var subtitle = $"guid {npc.Guid}   at {Format(npc.Position.X)}, {Format(npc.Position.Y)}, {Format(npc.Position.Z)}";

        ChatHelper.SendGmPanelData(player, "fields", "begin", kind, npc.Guid, title, subtitle);

        foreach (var field in fields)
            ChatHelper.SendGmPanelData(player, "fields", "f", field.Group, field.Label, field.Type, field.Value, field.Command, field.Options);

        ChatHelper.SendGmPanelData(player, "fields", "end");
    }

    private static IEnumerable<Field> GetNpcFields(Npc npc, string? eventKey)
    {
        foreach (var field in GetIdentityFields(npc))
            yield return field;

        yield return new("Identity", "Event key", "text", eventKey, "!npc event {v}", "none");

        foreach (var field in GetLookFields(npc, "!npc scale {v}", "!npc set tint {v}"))
            yield return field;

        yield return new("Behavior", "Attitude", "choice", npc.Disposition, "!npc disposition {v}", "Hostile=0=hostile;Neutral=1=neutral;Ally=2=ally");
        yield return new("Behavior", "Movement", "choice", npc.Behavior, "!npc behavior {v}", "Stay=Stay=stay;Follow=Follow=follow;Wander=Wander=wander");
        yield return new("Behavior", "Speed", "num", Format(npc.Speed), "!npc speed {v}");
        yield return new("Behavior", "Collision", "toggle", npc.Collidable ? 1 : 0, "!npc collide {v}");
        yield return new("Behavior", "Animation id", "num", npc.Animation, "!npc anim {v} loop");

        yield return new("Actions", "Move here", "action", null, "!npc move");
        yield return new("Actions", "Face me", "action", null, "!npc face");
        yield return new("Actions", "Go home", "action", null, "!npc home");
        yield return new("Actions", "Use", "action", null, "!npc use");
        yield return new("Actions", "Say", "text", string.Empty, "!npc say {v}");
        yield return new("Actions", "Despawn", "action", null, "!npc despawn");
    }

    private static IEnumerable<Field> GetPropFields(Npc npc)
    {
        foreach (var field in GetIdentityFields(npc))
            yield return field;

        foreach (var field in GetLookFields(npc, "!prop scale {v}", "!prop tint {v}"))
            yield return field;

        yield return new("Behavior", "Collision", "toggle", npc.Collidable ? 1 : 0, "!prop collide {v}");
        yield return new("Behavior", "Animation id", "num", npc.Animation, "!npc anim {v} loop");

        yield return new("Actions", "Move here", "action", null, "!prop move");
        yield return new("Actions", "Info", "action", null, "!prop info");
        yield return new("Actions", "Say", "text", string.Empty, "!npc say {v}");
        yield return new("Actions", "Remove", "action", null, "!prop remove");
    }

    private static IEnumerable<Field> GetIdentityFields(Npc npc)
    {
        yield return new("Identity", "Name", "text", npc.Name, "!npc name {v}", "clear");
        yield return new("Identity", "Name id", "num", npc.NameId, "!npc set nameid {v}");
        yield return new("Identity", "Subtitle id", "num", npc.SubTextNameId, "!npc set subtitle {v}");
        yield return new("Identity", "Nameplate", "toggle", npc.HideNamePlate ? 0 : 1, "!npc nameplate {v}");
        yield return new("Identity", "Nameplate image", "num", npc.NameplateImageId, "!npc set plateimage {v}");
        yield return new("Rotation", "Yaw", "steps", null, "!npc rotate yaw {v}", "-45;-15;15;45");
        yield return new("Rotation", "Pitch", "steps", null, "!npc rotate pitch {v}", "-45;-15;15;45");
        yield return new("Rotation", "Roll", "steps", null, "!npc rotate roll {v}", "-45;-15;15;45");
        yield return new("Rotation", "Level", "steps", null, "!npc rotate {v}", "reset");
        yield return new("Overhead icon", "Icons", "icons",
            string.Join(';', NpcProperties.GetIcons(npc).Select(x => $"{x.Type}:{x.IconId}:{x.State}")),
            "!npc icon add {v}");
    }

    private static IEnumerable<Field> GetLookFields(Npc npc, string scaleCommand, string tintCommand)
    {
        yield return new("Look", "Model id", "num", npc.ModelId, "!npc model {v}");
        yield return new("Look", "Scale", "num", Format(npc.Scale), scaleCommand);
        yield return new("Look", "Texture alias", "text", npc.TextureAlias, "!npc set texture {v}", "none");
        yield return new("Look", "Tint alias", "text", npc.TintAlias, "!npc set tintalias {v}", "none");
        yield return new("Look", "Tint id", "num", npc.TintId, tintCommand);
        yield return new("Look", "Effect id", "num", npc.CompositeEffectId, "!npc set effect {v}");
        yield return new("Look", "Height offset", "num", Format(npc.VerticalOffset), "!npc set offset {v}");
        yield return new("Look", "Image set", "num", npc.ImageSetId, "!npc set imageset {v}");
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
