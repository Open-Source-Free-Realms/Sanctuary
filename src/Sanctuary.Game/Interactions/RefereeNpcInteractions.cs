using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// The menu referees get when they click an NPC. Labels are client string ids, so only strings the
/// client already has can be used; anything free-form (names, ids, values) goes through !npc, which
/// acts on the NPC selected here.
/// </summary>
public sealed class RefereeNpcInteractions : IInteractionProvider
{
    private const int IconId = 133;

    private static InteractionData Entry(int buttonText) => new()
    {
        Id = IInteraction.UniqueId++,
        IconId = IconId,
        ButtonText = buttonText
    };

    // Main menu
    private static readonly InteractionData Info = Entry(429840);        // "Info"
    private static readonly InteractionData Rename = Entry(556);         // "Rename"
    private static readonly InteractionData Emotes = Entry(8083);        // "Emotes"
    private static readonly InteractionData Movement = Entry(7495);      // "Movement"
    private static readonly InteractionData Settings = Entry(36259);     // "Settings"
    private static readonly InteractionData MoveHere = Entry(569);       // "Move"
    private static readonly InteractionData FaceMe = Entry(430609);      // "Turn"
    private static readonly InteractionData Use = Entry(432156);         // "Interact"
    private static readonly InteractionData Remove = Entry(18704);       // "Remove"

    private static readonly InteractionData Back = Entry(1861);          // "Back"

    // Props placed with !place (world fixtures)
    private static readonly InteractionData PropInfo = Entry(429840);    // "Info"
    private static readonly InteractionData PropMove = Entry(569);       // "Move"
    private static readonly InteractionData PropRemove = Entry(18704);   // "Remove"

    // Movement
    private static readonly InteractionData Follow = Entry(421551);      // "Follow me."
    private static readonly InteractionData Stay = Entry(422303);        // "Stay here."
    private static readonly InteractionData Wander = Entry(18349);       // "Wander Test"
    private static readonly InteractionData ReturnHome = Entry(118005);  // "Return"

    // Settings
    private static readonly InteractionData Friendly = Entry(8078);      // "Friendly"
    private static readonly InteractionData Neutral = Entry(130598);     // "Normal"
    private static readonly InteractionData Hostile = Entry(22498);      // "Attack"
    private static readonly InteractionData Small = Entry(4087);         // "Small"
    private static readonly InteractionData DefaultSize = Entry(412129); // "Default"
    private static readonly InteractionData Large = Entry(4089);         // "Large"
    private static readonly InteractionData ToggleName = Entry(442283);  // "Hide"

    /// <summary>
    /// Label string id and animation id (AnimationTypes.xml) for each emote.
    /// </summary>
    private static readonly (InteractionData Data, int AnimationId, bool Loop)[] EmoteEntries =
    [
        (Entry(23475), 3333, false),  // "Wave" - emo_wave
        (Entry(60), 3501, true),      // "Dance" - emo_dance_freestyle_01
        (Entry(23473), 3306, false),  // "Cheer" - emo_cheer
        (Entry(23489), 3316, false),  // "Laugh" - emo_laugh
        (Entry(420181), 3342, false), // "Bow" - emo_bow
        (Entry(36714), 3319, false),  // "Point" - emo_point
        (Entry(3300), 3101, false),   // "Talk" - emo_talk_basic
        (Entry(4885), 7, true),       // "Sit Down" - loc_sit
        (Entry(23477), 3329, true),   // "Sleep" - emo_sleep_standing
        (Entry(21829), 1, true)       // "Idle" - back to the default idle
    ];

    public static readonly InteractionData[] MainMenu = [Info, Rename, Emotes, Movement, Settings, MoveHere, FaceMe, Use, Remove];
    public static readonly InteractionData[] PropMenu = [PropInfo, PropMove, PropRemove];
    private static readonly InteractionData[] EmoteMenu = [.. EmoteEntries.Select(x => x.Data), Back];
    private static readonly InteractionData[] MovementMenu = [Follow, Stay, Wander, ReturnHome, Back];
    private static readonly InteractionData[] SettingsMenu = [Friendly, Neutral, Hostile, Small, DefaultSize, Large, ToggleName, Back];

    private readonly ILogger _auditLogger;

    public IEnumerable<IInteraction> Interactions { get; }

    public RefereeNpcInteractions(ILoggerFactory loggerFactory)
    {
        _auditLogger = loggerFactory.CreateLogger("Audit");

        var interactions = new List<IInteraction>
        {
            Create(Info, (player, npc) => SendInfo(player, npc)),
            Create(Rename, (player, npc) => ChatHelper.SendSystemMessage(player, $"Type !npc name <new name> to rename {Describe(npc)} for everyone.")),
            Create(Emotes, (player, npc) => ShowMenu(player, npc, EmoteMenu)),
            Create(Movement, (player, npc) => ShowMenu(player, npc, MovementMenu)),
            Create(Settings, (player, npc) => ShowMenu(player, npc, SettingsMenu)),
            Create(Back, (player, npc) => ShowMenu(player, npc, MainMenu)),
            Create(MoveHere, MoveToReferee),
            Create(FaceMe, FaceReferee),
            Create(Use, UseDefaultInteraction),
            Create(Remove, RemoveNpc),

            Create(Follow, (player, npc) => SetBehavior(player, npc, NpcBehavior.Follow)),
            Create(Stay, (player, npc) => SetBehavior(player, npc, NpcBehavior.Stay)),
            Create(Wander, (player, npc) => SetBehavior(player, npc, NpcBehavior.Wander)),
            Create(ReturnHome, ReturnToSpawn),

            Create(Friendly, (player, npc) => SetDisposition(player, npc, 2)),
            Create(Neutral, (player, npc) => SetDisposition(player, npc, 1)),
            Create(Hostile, (player, npc) => SetDisposition(player, npc, 0)),
            Create(Small, (player, npc) => SetScale(player, npc, 0.5f)),
            Create(DefaultSize, (player, npc) => SetScale(player, npc, 1f)),
            Create(Large, (player, npc) => SetScale(player, npc, 2f)),
            Create(ToggleName, ToggleNameplate),

            Create(PropInfo, SendPropInfo),
            Create(PropMove, MoveProps),
            Create(PropRemove, RemoveProp)
        };

        foreach (var (data, animationId, loop) in EmoteEntries)
            interactions.Add(Create(data, (player, npc) => PlayAnimation(player, npc, animationId, loop)));

        Interactions = interactions;
    }

    /// <summary>
    /// Mounts belong to players, collection nodes to their pools and house furniture to its house's own tools,
    /// so none of them is editable here.
    /// </summary>
    public static bool CanEdit(Npc npc) => npc is not Mount and not CollectionNode && !npc.IsHouseFixture;

    /// <summary>
    /// The menu a referee gets for this NPC: the prop menu for placed props, otherwise its own
    /// interactions followed by the NPC editing menu.
    /// </summary>
    public static IEnumerable<InteractionData> GetMenu(Npc npc)
    {
        if (npc.Zone is WorldZone worldZone && worldZone.IsWorldFixture(npc.Guid))
            return PropMenu;

        return [.. npc.Interactions, .. MainMenu];
    }

    private DelegateInteraction Create(InteractionData data, Action<Player, Npc> onInteract)
    {
        return new DelegateInteraction(data, (player, other) =>
        {
            if (!player.IsInGodMode || other is not Npc npc || !CanEdit(npc))
                return;

            player.SelectedNpcGuid = npc.Guid;
            onInteract(player, npc);

            RefereeEditorHelper.Refresh(player);
        });
    }

    private static void ShowMenu(Player player, Npc npc, IEnumerable<InteractionData> menu)
    {
        player.SendTunneled(InteractionMenuHelper.GetInteractionListPacket(npc.Guid, npc.Name, menu));
    }

    private static string Describe(Npc npc) => $"{npc.Name ?? $"NameId {npc.NameId}"} (guid {npc.Guid})";

    private void LogAction(Player player, Npc npc, string action, string? detail = null)
    {
        _auditLogger.LogInformation("npcmenu|{Action}|Actor: \"{ActorName}\" ({ActorGuid}), Target: \"{Target}\"{Detail}",
            action, player.Name, player.Guid, Describe(npc), detail is null ? string.Empty : $", {detail}");
    }

    private static void SendInfo(Player player, Npc npc)
    {
        ChatHelper.SendSystemMessage(player,
            $"{Describe(npc)}: Model {npc.ModelId}, Scale {npc.Scale}, Animation {npc.Animation}, " +
            $"Disposition {npc.Disposition}, Behavior {npc.Behavior}, Nameplate {(npc.HideNamePlate ? "hidden" : "shown")}, " +
            $"Position ({npc.Position.X:F1}, {npc.Position.Y:F1}, {npc.Position.Z:F1}). Edit with !npc.");
    }

    private void MoveToReferee(Player player, Npc npc)
    {
        npc.SetBehavior(NpcBehavior.Stay);
        npc.UpdatePosition(player.Position, player.Rotation);

        WorldNpcHelper.SavePosition(npc);

        LogAction(player, npc, "Move");
    }

    private void FaceReferee(Player player, Npc npc)
    {
        if (npc.FaceTowards(player.Position))
            WorldNpcHelper.SavePosition(npc);
    }

    private static void UseDefaultInteraction(Player player, Npc npc)
    {
        if (npc.InteractAction is null)
        {
            ChatHelper.SendSystemMessage(player, $"{Describe(npc)} has no default interaction.");
            return;
        }

        npc.InteractAction(player);
    }

    private void RemoveNpc(Player player, Npc npc)
    {
        LogAction(player, npc, "Remove");

        player.SelectedNpcGuid = null;
        WorldNpcHelper.Remove(npc);

        ChatHelper.SendSystemMessage(player, $"Removed {Describe(npc)}.");
    }

    private void SetBehavior(Player player, Npc npc, NpcBehavior behavior)
    {
        npc.SetBehavior(behavior, player.Guid);
        WorldNpcHelper.Save(npc);

        LogAction(player, npc, "Behavior", behavior.ToString());
        ChatHelper.SendSystemMessage(player, $"{Describe(npc)} behavior set to {behavior}.");
    }

    private void ReturnToSpawn(Player player, Npc npc)
    {
        if (npc.SpawnPosition == default)
        {
            ChatHelper.SendSystemMessage(player, $"{Describe(npc)} has no spawn position.");
            return;
        }

        npc.SetBehavior(NpcBehavior.Stay);
        npc.MoveTo(new Vector3(npc.SpawnPosition.X, npc.SpawnPosition.Y, npc.SpawnPosition.Z));

        LogAction(player, npc, "Return");
    }

    private void SetDisposition(Player player, Npc npc, int disposition)
    {
        npc.Disposition = disposition;
        npc.RefreshForVisiblePlayers();
        WorldNpcHelper.Save(npc);

        LogAction(player, npc, "Disposition", disposition.ToString());
    }

    private void SetScale(Player player, Npc npc, float scale)
    {
        npc.Scale = scale;
        npc.RefreshForVisiblePlayers();
        WorldNpcHelper.Save(npc);

        LogAction(player, npc, "Scale", scale.ToString());
    }

    private void ToggleNameplate(Player player, Npc npc)
    {
        npc.HideNamePlate = !npc.HideNamePlate;
        npc.RefreshForVisiblePlayers();
        WorldNpcHelper.Save(npc);

        LogAction(player, npc, "Nameplate", npc.HideNamePlate ? "hidden" : "shown");
    }

    private static void SendPropInfo(Player player, Npc npc)
    {
        if (npc.Zone is not WorldZone worldZone || !worldZone.TryGetWorldFixture(npc.Guid, out var fixture))
            return;

        ChatHelper.SendSystemMessage(player,
            $"Prop #{fixture.Id}: item {fixture.Definition}, tint {fixture.Tint}, scale {fixture.Scale}, " +
            $"position ({fixture.PositionX:F1}, {fixture.PositionY:F1}, {fixture.PositionZ:F1}), guid {npc.Guid}.");
    }

    private void MoveProps(Player player, Npc npc)
    {
        if (npc.Zone is not WorldZone worldZone)
            return;

        // Already stamping with !place mode keeps that session; it can move props too.
        if (!worldZone.IsPlacing(player) && !worldZone.TryBeginMoving(player))
            return;

        LogAction(player, npc, "MoveProps");
        ChatHelper.SendSystemMessage(player, "Furniture tool open: drag props to move them, pick one up to delete it. !place off to stop.");
    }

    private void RemoveProp(Player player, Npc npc)
    {
        if (npc.Zone is not WorldZone worldZone || !worldZone.TryRemoveWorldFixture(npc.Guid, out var fixture))
            return;

        LogAction(player, npc, "RemoveProp", $"Prop: {fixture.Id}, Item: {fixture.Definition}");
        ChatHelper.SendSystemMessage(player, $"Removed prop #{fixture.Id} (item {fixture.Definition}).");
    }

    private void PlayAnimation(Player player, Npc npc, int animationId, bool loop)
    {
        npc.PlayAnimation(animationId, loop);

        if (loop)
            WorldNpcHelper.Save(npc);

        LogAction(player, npc, "Animation", $"{animationId}{(loop ? " (loop)" : string.Empty)}");
    }
}
