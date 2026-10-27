using System.Globalization;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Feeds the GM panel's editor (ClientMods/chatWindowCtrl) with the entity a referee selected, as hidden
/// <c>[[SANCTUARY_GM]]|edit|...</c> lines. The editor's controls answer with !npc and !prop commands.
/// </summary>
public static class RefereeEditorHelper
{
    /// <summary>
    /// Animation id (AnimationTypes.xml), label, and whether it loops as the NPC's idle rather than playing once.
    /// </summary>
    private static readonly (int Id, string Label, bool Loop)[] Animations =
    [
        (1, "Idle", true),
        (3333, "Wave", false),
        (3306, "Cheer", false),
        (3316, "Laugh", false),
        (3342, "Bow", false),
        (3344, "Salute", false),
        (3319, "Point", false),
        (3335, "Yes", false),
        (3317, "No", false),
        (3326, "Shrug", false),
        (3311, "Flex", false),
        (3301, "Angry", false),
        (3325, "Sad", false),
        (3309, "Cry", false),
        (3101, "Talk", false),
        (3102, "Talk (ask)", false),
        (3108, "Talk (happy)", false),
        (3111, "Talk (angry)", false),
        (3501, "Dance", true),
        (3521, "Slide dance", true),
        (35010, "Chicken dance", true),
        (7, "Sit", true),
        (3329, "Sleep", true),
        (3328, "Sleep (sitting)", true)
    ];

    /// <summary>
    /// Selects the entity and sends it to the editor; null clears the selection.
    /// </summary>
    public static void Select(Player player, Npc? npc)
    {
        player.SelectedNpcGuid = npc?.Guid;

        // anims: "<id>:<label>:<loop 1/0>" per entry.
        if (npc is not null)
            ChatHelper.SendGmPanelData(player, "anims", [.. Animations.Select(x => $"{x.Id}:{x.Label}:{(x.Loop ? 1 : 0)}")]);

        Send(player, npc);
    }

    /// <summary>
    /// Re-sends the selection after an edit so the editor shows current values.
    /// </summary>
    public static void Refresh(Player player)
    {
        if (!player.IsInGodMode)
            return;

        Npc? npc = null;

        if (player.SelectedNpcGuid is { } guid && player.Zone.TryGetNpc(guid, out var selected))
            npc = selected;

        Send(player, npc);
    }

    private static void Send(Player player, Npc? npc)
    {
        if (npc is null)
        {
            ChatHelper.SendGmPanelData(player, "edit", "none");
            return;
        }

        if (npc.Zone is WorldZone worldZone && worldZone.TryGetWorldFixture(npc.Guid, out var fixture))
        {
            // prop: guid, fixture id, item id, name, scale, tint, x, y, z, collidable
            ChatHelper.SendGmPanelData(player, "edit", "prop",
                npc.Guid, fixture.Id, fixture.Definition, npc.Name ?? string.Empty,
                Format(fixture.Scale), fixture.Tint,
                Format(npc.Position.X), Format(npc.Position.Y), Format(npc.Position.Z),
                npc.Collidable ? 1 : 0);
            return;
        }

        var eventKey = npc.Zone is WorldZone eventZone ? eventZone.GetWorldNpcEvent(npc.Guid) : null;

        // npc: guid, name, name id, model, scale, animation, disposition, nameplate, behavior, speed, event, x, y, z, collidable
        ChatHelper.SendGmPanelData(player, "edit", "npc",
            npc.Guid, npc.Name ?? string.Empty, npc.NameId, npc.ModelId,
            Format(npc.Scale), npc.Animation, npc.Disposition, npc.HideNamePlate ? 0 : 1,
            npc.Behavior, Format(npc.Speed), eventKey ?? string.Empty,
            Format(npc.Position.X), Format(npc.Position.Y), Format(npc.Position.Z),
            npc.Collidable ? 1 : 0);
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
