using System;
using System.Linq;

using Sanctuary.Game.Conversations;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Authors the conversation on the selected world NPC. Text arguments are escaped (see <see cref="PanelTextHelper"/>).
/// Every edit is saved and the tree is sent back to the GM panel as conv/convnode/convchoice/convend lines.
/// </summary>
public class ConvChatCommand : IRawChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "conv";
    public string Usage => "show | node <id> <text> | node del <id> | choice add <node> <next> <text> | choice del <node> <index> " +
        "| choice give <node> <index> <itemId> [qty] | choice hook <node> <index> <hook|none> | start <node> | clear | copy <npcGuid> | test";
    public string Description => "Edits the conversation on the NPC you last clicked. A choice with next 0 ends it.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ConvChatCommand(IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        if (invoker.Zone is not WorldZone worldZone
            || invoker.SelectedNpcGuid is not { } guid
            || !worldZone.IsWorldNpc(guid)
            || !worldZone.TryGetNpc(guid, out var npc))
        {
            ChatHelper.SendSystemMessage(invoker, "Click an NPC saved in a world zone first.");
            return true;
        }

        var action = args[0].ToLowerInvariant();

        switch (action)
        {
            case "show":
                SendTree(invoker, npc);
                return true;

            case "test":
                if (!worldZone.NpcContent.StartConversation(invoker, npc))
                    ChatHelper.SendSystemMessage(invoker, "This NPC has no conversation, or its start node is missing.");
                return true;

            case "clear":
                worldZone.TrySaveConversation(npc, null);
                break;

            case "copy":
                if (args.Length != 2 || !ulong.TryParse(args[1], out var sourceGuid))
                    return false;

                if (!worldZone.TryGetNpc(sourceGuid, out var source) || source.Conversation is null)
                {
                    ChatHelper.SendSystemMessage(invoker, $"No NPC with a conversation and guid {sourceGuid} in your zone.");
                    return true;
                }

                worldZone.TrySaveConversation(npc, source.Conversation.Clone());
                break;

            default:
                var conversation = npc.Conversation?.Clone() ?? new Conversation();

                if (!TryEdit(conversation, action, args[1..], rawArgs, out var error))
                {
                    if (error is null)
                        return false;

                    ChatHelper.SendSystemMessage(invoker, error);
                    return true;
                }

                worldZone.TrySaveConversation(npc, conversation);
                break;
        }

        _chatCommandManager.LogAction(this, invoker, action, null, $"Guid: {npc.Guid}");
        SendTree(invoker, npc);

        return true;
    }

    /// <summary>
    /// Applies an edit to the copy. False with a null error means bad usage.
    /// </summary>
    /// <param name="rawArgs">Everything after "conv" as typed; texts are taken from it so spaces survive.</param>
    private bool TryEdit(Conversation conversation, string action, string[] args, string rawArgs, out string? error)
    {
        error = null;

        switch (action)
        {
            case "start":
                if (args.Length != 1 || !int.TryParse(args[0], out var startId))
                    return false;

                conversation.Start = startId;
                return true;

            case "node":
                if (args.Length == 2 && args[0].Equals("del", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(args[1], out var deleteId))
                        return false;

                    conversation.Nodes.RemoveAll(x => x.Id == deleteId);
                    return true;
                }

                if (args.Length < 2 || !int.TryParse(args[0], out var nodeId) || nodeId < 1)
                    return false;

                // "node <id> <text>"
                var text = PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 2));

                if (text.Length > Conversation.MaxTextLength)
                {
                    error = $"Node text is at most {Conversation.MaxTextLength} characters.";
                    return false;
                }

                var node = conversation.GetNode(nodeId);

                if (node is null)
                {
                    if (conversation.Nodes.Count >= Conversation.MaxNodes)
                    {
                        error = $"Conversations have at most {Conversation.MaxNodes} nodes.";
                        return false;
                    }

                    node = new ConversationNode { Id = nodeId };
                    conversation.Nodes.Add(node);
                }

                node.Text = text;
                return true;

            case "choice":
                return TryEditChoice(conversation, args, rawArgs, out error);

            default:
                return false;
        }
    }

    private bool TryEditChoice(Conversation conversation, string[] args, string rawArgs, out string? error)
    {
        error = null;

        if (args.Length < 3 || !int.TryParse(args[1], out var nodeId))
            return false;

        var node = conversation.GetNode(nodeId);

        if (node is null)
        {
            error = $"No node {nodeId}; add it with !{KeyWord} node {nodeId} <text>.";
            return false;
        }

        var subAction = args[0].ToLowerInvariant();

        if (subAction == "add")
        {
            if (args.Length < 4 || !int.TryParse(args[2], out var next) || next < 0)
                return false;

            if (node.Choices.Count >= Conversation.MaxChoicesPerNode)
            {
                error = $"Nodes have at most {Conversation.MaxChoicesPerNode} choices.";
                return false;
            }

            // "choice add <node> <next> <text>"
            var text = PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 4));

            if (text.Length > Conversation.MaxTextLength)
            {
                error = $"Choice text is at most {Conversation.MaxTextLength} characters.";
                return false;
            }

            node.Choices.Add(new ConversationChoice { Text = text, Next = next });
            return true;
        }

        if (!int.TryParse(args[2], out var index))
            return false;

        if (index < 0 || index >= node.Choices.Count)
        {
            error = $"Node {nodeId} has no choice {index}.";
            return false;
        }

        var choice = node.Choices[index];

        switch (subAction)
        {
            case "del":
                node.Choices.RemoveAt(index);
                return true;

            case "give":
                if (args.Length is < 4 or > 5 || !int.TryParse(args[3], out var itemId) || itemId < 0)
                    return false;

                var quantity = 1;

                if (args.Length == 5 && (!int.TryParse(args[4], out quantity) || quantity is < 1 or > 999))
                    return false;

                if (itemId != 0 && !_resourceManager.ClientItemDefinitions.ContainsKey(itemId))
                {
                    error = $"Unknown item {itemId}.";
                    return false;
                }

                choice.GiveItemId = itemId;
                choice.GiveQuantity = quantity;
                return true;

            case "hook":
                if (args.Length != 4)
                    return false;

                choice.Hook = args[3].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[3];
                return true;

            default:
                return false;
        }
    }

    private static void SendTree(Player invoker, Npc npc)
    {
        var conversation = npc.Conversation;

        ChatHelper.SendGmPanelData(invoker, "conv", npc.Guid, conversation?.Start ?? 1);

        foreach (var node in conversation?.Nodes.OrderBy(x => x.Id) ?? Enumerable.Empty<ConversationNode>())
        {
            ChatHelper.SendGmPanelData(invoker, "convnode", node.Id, PanelTextHelper.Escape(node.Text));

            for (var i = 0; i < node.Choices.Count; i++)
            {
                var choice = node.Choices[i];

                ChatHelper.SendGmPanelData(invoker, "convchoice", node.Id, i, choice.Next, choice.GiveItemId, choice.GiveQuantity,
                    choice.Hook ?? string.Empty, PanelTextHelper.Escape(choice.Text));
            }
        }

        ChatHelper.SendGmPanelData(invoker, "convend");
    }
}
