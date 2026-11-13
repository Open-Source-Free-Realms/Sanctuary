using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.MiniGames;

// Handlers and zone ticks run on different threads,
// so all session changes go through one lock.
public sealed class MiniGameManager : IMiniGameManager
{
    private const int CombatJobSwitchedMessageId = 634;
    private const int MiniGameJobSwitchedMessageId = 4594;

    private readonly object _lock = new();

    private readonly IResourceManager _resourceManager;
    private readonly IEnumerable<IMiniGameFactory> _factories;
    private readonly IMiniGameClient _client;

    public MiniGameManager(IResourceManager resourceManager, IEnumerable<IMiniGameFactory> factories, IMiniGameClient client)
    {
        _resourceManager = resourceManager;
        _factories = factories;
        _client = client;
    }

    public bool ShowDetails(Player player, int activityId)
    {
        if (!_resourceManager.Activities.TryGet(activityId, out var activity) || !activity.PlayerCanJoin)
            return false;

        lock (_lock)
        {
            if (player.MiniGame is { } current)
            {
                // Browsing another activity replaces the details screen; a game in progress does not.
                if (!current.TryGetParticipant(player, out var participant) || participant.State != MiniGameUiState.Details
                    || current.Definition.GameId == activityId)
                    return false;

                current.Leave(player, MiniGameEndReason.Aborted);
            }

            if (!ActivityHelper.TryGetRequiredProfile(player, _resourceManager, activity, out var requiredProfile))
                return false;

            if (requiredProfile is not null && requiredProfile.Type != player.ActiveProfile.Type)
            {
                _client.ProfileSwitched(player, requiredProfile,
                    requiredProfile.Type == (int)ProfileType.Combat ? CombatJobSwitchedMessageId : MiniGameJobSwitchedMessageId);
            }

            var definition = ActivityHelper.GetMiniGameUiDefinition(_resourceManager, activity);

            MiniGameSession? session = null;

            foreach (var factory in _factories)
            {
                if (factory.TryCreate(activity, definition, out session))
                    break;
            }

            return OpenLocked(player, session ?? new UnavailableMiniGameSession(definition));
        }
    }

    public bool Open(Player player, MiniGameSession session)
    {
        lock (_lock)
        {
            if (player.MiniGame is not null)
                return false;

            return OpenLocked(player, session);
        }
    }

    private bool OpenLocked(Player player, MiniGameSession session)
    {
        session.Client = _client;
        session.Released = released =>
        {
            if (released.MiniGame == session)
                released.MiniGame = null;
        };

        player.MiniGame = session;

        session.Open(player);

        return true;
    }

    public void RefreshDetails(Player player)
    {
        lock (_lock)
            player.MiniGame?.RefreshDetails(player);
    }

    public bool Enter(Player player, int sessionId)
    {
        lock (_lock)
        {
            if (player.MiniGame is not { } session || session.Id != sessionId)
                return false;

            var playable = session.Playable;

            if (session.Enter(player))
                return true;

            if (!playable)
                _client.Unavailable(player);

            return false;
        }
    }

    public void ClientIsReady(Player player)
    {
        lock (_lock)
            player.MiniGame?.ClientIsReady(player);
    }

    public bool Leave(Player player)
    {
        lock (_lock)
        {
            if (player.MiniGame is not { } session || !session.TryGetParticipant(player, out var participant))
                return false;

            var reason = participant.State == MiniGameUiState.Ended ? MiniGameEndReason.Finished : MiniGameEndReason.Aborted;

            session.Leave(player, reason);

            return true;
        }
    }

    public bool Finish(Player player, MiniGameResult result)
    {
        lock (_lock)
            return player.MiniGame?.Finish(result) ?? false;
    }

    public bool End(Player player)
    {
        lock (_lock)
        {
            if (player.MiniGame is not { } session)
                return false;

            session.End();

            return true;
        }
    }

    public void Disconnect(Player player)
    {
        lock (_lock)
            player.MiniGame?.Leave(player, MiniGameEndReason.Disconnected);
    }
}
