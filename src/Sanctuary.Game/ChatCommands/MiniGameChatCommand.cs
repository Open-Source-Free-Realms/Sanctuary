using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.MiniGames.Testing;

namespace Sanctuary.Game.ChatCommands;

public class MiniGameChatCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IMiniGameManager _miniGameManager;

    public string KeyWord => "minigame";

    public string Usage => "start <activityId> | join <activityId> | go | finish [stars] | end | abort | status";

    public string Description => "Opens a test minigame (start) or the real one (join) for an activity and steps it through start, finish, end and abort.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public MiniGameChatCommand(IResourceManager resourceManager, IMiniGameManager miniGameManager)
    {
        _resourceManager = resourceManager;
        _miniGameManager = miniGameManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                if (args.Length < 2 || !int.TryParse(args[1], out var activityId))
                    return false;

                Start(invoker, activityId);
                return true;

            case "join":
                if (args.Length < 2 || !int.TryParse(args[1], out var joinActivityId))
                    return false;

                // Same path as joining from the Activities window or a sign-up NPC.
                Reply(invoker, _miniGameManager.ShowDetails(invoker, joinActivityId),
                    $"Opened activity {joinActivityId}.",
                    $"Could not open activity {joinActivityId}. It may not exist, not be joinable, or you are already in a game.");
                return true;

            case "go":
                if (invoker.MiniGame is not { } session)
                {
                    ChatHelper.SendSystemMessage(invoker, "You are not in a minigame. Use !minigame start <activityId> first.");
                    return true;
                }

                Reply(invoker, _miniGameManager.Enter(invoker, session.Id),
                    "Entered the minigame.", "Your minigame has already started.");
                return true;

            case "finish":
                var stars = 3;

                if (args.Length > 1 && !int.TryParse(args[1], out stars))
                    return false;

                Reply(invoker, _miniGameManager.Finish(invoker, new MiniGameResult { EarnedStars = stars }),
                    $"Finished with {stars} stars.", "Your minigame is not running.");
                return true;

            case "end":
                Reply(invoker, _miniGameManager.End(invoker), "Ended the minigame for everyone.", "You are not in a minigame.");
                return true;

            case "abort":
                Reply(invoker, _miniGameManager.Leave(invoker), "Left the minigame.", "You are not in a minigame.");
                return true;

            case "status":
                Status(invoker);
                return true;

            default:
                return false;
        }
    }

    private void Start(Player invoker, int activityId)
    {
        if (!_resourceManager.Activities.TryGet(activityId, out var activity))
        {
            ChatHelper.SendSystemMessage(invoker, $"Activity {activityId} does not exist.");
            return;
        }

        var definition = ActivityHelper.GetMiniGameUiDefinition(_resourceManager, activity);

        Reply(invoker, _miniGameManager.Open(invoker, new TestMiniGameSession(definition)),
            $"Opened test minigame for activity {activityId}. Press Play or use !minigame go.",
            "You are already in a minigame. Use !minigame abort first.");
    }

    private static void Status(Player invoker)
    {
        if (invoker.MiniGame is not { } session)
        {
            ChatHelper.SendSystemMessage(invoker, "You are not in a minigame.");
            return;
        }

        var participantState = session.TryGetParticipant(invoker, out var participant) ? participant.State.ToString() : "none";

        ChatHelper.SendSystemMessage(invoker,
            $"{session.GetType().Name} #{session.Id}, game {session.Definition.GameId}: {session.State}, you: {participantState}, players: {session.Participants.Count}");

        if (session is TestMiniGameSession test && test.Events.Count > 0)
            ChatHelper.SendSystemMessage(invoker, "Hooks: " + string.Join(", ", test.Events.TakeLast(8)));
    }

    private static void Reply(Player invoker, bool succeeded, string success, string failure)
        => ChatHelper.SendSystemMessage(invoker, succeeded ? success : failure);
}
