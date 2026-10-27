using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

public class NpcContentManager : INpcContentManager
{
    private const float ProximityRange = 8f;

    /// <summary>
    /// Leaving proximity needs a little more distance than entering, so standing on the edge doesn't spam.
    /// </summary>
    private const float ProximityExitRange = 10f;

    private const float ConversationRange = 20f;

    private readonly ILogger _logger;
    private readonly IRewardManager _rewardManager;
    private readonly IQuestManager _questManager;

    public NpcContentManager(ILogger<NpcContentManager> logger, IRewardManager rewardManager, IQuestManager questManager)
    {
        _logger = logger;
        _rewardManager = rewardManager;
        _questManager = questManager;
    }

    #region Scripts

    public async Task<string?> SetScriptAsync(Npc npc, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            npc.GmScript = null;
            UpdateInteraction(npc);
            return null;
        }

        var (sandbox, error) = await ScriptSandbox.CompileAsync(npc, source, $"npc {npc.Guid}");

        if (sandbox is null)
            return error;

        npc.GmScript = sandbox;

        lock (npc.NearbyPlayers)
            npc.NearbyPlayers.Clear();

        UpdateInteraction(npc);

        var spawnError = await sandbox.CallAsync("onSpawn");

        return spawnError is null ? null : $"Saved, but onSpawn failed: {spawnError}";
    }

    public async Task<string?> TestScriptAsync(Npc npc, Player player)
    {
        if (npc.GmScript is not { } sandbox)
            return "This has no script.";

        return await sandbox.CallAsync("onSpawn")
            ?? await sandbox.CallAsync("onInteract", new ScriptPlayer(player, _rewardManager, _questManager));
    }

    private void FireHook(Npc npc, string hook, Player? player = null, string? argument = null)
    {
        if (npc.GmScript is not { } sandbox || !sandbox.HasHook(hook))
            return;

        _ = RunHookAsync(npc, sandbox, hook, player, argument);
    }

    private async Task RunHookAsync(Npc npc, ScriptSandbox sandbox, string hook, Player? player, string? argument)
    {
        var scriptPlayer = player is null ? null : new ScriptPlayer(player, _rewardManager, _questManager);

        var error = await sandbox.CallAsync(hook, scriptPlayer, argument);

        if (error is not null)
            _logger.LogWarning("Script on NPC {NpcGuid} failed in {Hook}: {Error}", npc.Guid, hook, error);
    }

    #endregion

    #region Interaction

    public void SetConversation(Npc npc, Conversation? conversation)
    {
        npc.Conversation = conversation;
        UpdateInteraction(npc);
    }

    /// <summary>
    /// Makes the NPC clickable while it has an onInteract hook or a conversation. Props also get the
    /// cursor they otherwise lack, so players can click them.
    /// </summary>
    private void UpdateInteraction(Npc npc)
    {
        var interactive = npc.GmScript?.HasHook("onInteract") == true || npc.Conversation is not null;

        npc.InteractAction = interactive ? player => OnInteract(npc, player) : null;

        if (npc.IsWorldProp)
        {
            npc.IsInteractable = interactive;
            npc.HasCursor = interactive ? true : null;
            npc.CursorId = interactive ? InteractionMenuHelper.RefereeCursorId : (byte)0;
        }

        npc.RefreshForVisiblePlayers();
    }

    public void OnInteract(Npc npc, Player player)
    {
        if (!InteractionMenuHelper.CanInteract(npc, player))
            return;

        // A script that handles clicks decides for itself whether to start the conversation.
        if (npc.GmScript?.HasHook("onInteract") == true)
        {
            FireHook(npc, "onInteract", player);
            return;
        }

        StartConversation(player, npc);
    }

    public void OnSecond(Npc npc)
    {
        FireHook(npc, "onSecond");

        foreach (var player in npc.VisiblePlayers.Values)
        {
            var distance = Distance(npc, player);

            if (npc.GmScript?.HasHook("onProximity") == true)
            {
                bool entered;

                lock (npc.NearbyPlayers)
                {
                    entered = distance <= ProximityRange && npc.NearbyPlayers.Add(player.Guid);

                    if (distance > ProximityExitRange)
                        npc.NearbyPlayers.Remove(player.Guid);
                }

                if (entered)
                    FireHook(npc, "onProximity", player);
            }

            if (player.ActiveConversation?.NpcGuid == npc.Guid && distance > ConversationRange)
                EndConversation(player);
        }

        // Forget players who left view, so they trigger again when they come back.
        lock (npc.NearbyPlayers)
            npc.NearbyPlayers.RemoveWhere(guid => !npc.VisiblePlayers.ContainsKey(guid));
    }

    #endregion

    #region Conversations

    public bool StartConversation(Player player, Npc npc, int? nodeId = null)
    {
        var node = npc.Conversation?.GetNode(nodeId ?? npc.Conversation.Start);

        if (node is null)
            return false;

        player.ActiveConversation = (npc.Guid, node.Id);

        // dialog: npc guid, npc name, node id, text, then "<index>:<choice text>" per choice.
        ChatHelper.SendGmPanelData(player, "dialog",
        [
            npc.Guid,
            PanelTextHelper.Escape(npc.Name ?? string.Empty),
            node.Id,
            PanelTextHelper.Escape(node.Text),
            .. node.Choices.Select((choice, index) => $"{index}:{PanelTextHelper.Escape(choice.Text)}")
        ]);

        // Lets everyone nearby see what the NPC says.
        npc.Say(node.Text);

        return true;
    }

    public void Choose(Player player, ulong npcGuid, int nodeId, int index)
    {
        if (player.ActiveConversation is not { } active || active.NpcGuid != npcGuid || active.NodeId != nodeId)
            return;

        if (!player.Zone.TryGetNpc(npcGuid, out var npc) || Distance(npc, player) > ConversationRange)
        {
            EndConversation(player);
            return;
        }

        var choice = npc.Conversation?.GetNode(nodeId)?.Choices.ElementAtOrDefault(index);

        if (choice is null)
            return;

        if (choice.GiveItemId != 0)
            _rewardManager.TryGrantItem(player, choice.GiveItemId, 0, choice.GiveQuantity, npc.Guid);

        if (!string.IsNullOrEmpty(choice.Hook))
            FireHook(npc, "onChoice", player, choice.Hook);

        if (choice.Next == 0 || !StartConversation(player, npc, choice.Next))
            EndConversation(player);
    }

    public void EndConversation(Player player)
    {
        if (player.ActiveConversation is null)
            return;

        player.ActiveConversation = null;

        ChatHelper.SendGmPanelData(player, "dialog", "close");
    }

    #endregion

    private static float Distance(Npc npc, Player player) => Vector3.Distance(
        new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z),
        new Vector3(player.Position.X, player.Position.Y, player.Position.Z));
}
