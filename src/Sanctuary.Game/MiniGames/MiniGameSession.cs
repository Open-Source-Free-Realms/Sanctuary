using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames;

public abstract class MiniGameSession
{
    private static int _nextId;

    private readonly Dictionary<ulong, MiniGameUiSession> _participants = [];

    protected MiniGameSession(MiniGameUiDefinition definition, IMiniGameHost host)
    {
        Id = Interlocked.Increment(ref _nextId);
        Definition = definition;
        Host = host;
    }

    public int Id { get; }
    public MiniGameUiDefinition Definition { get; }
    public IMiniGameHost Host { get; }
    public MiniGameState State { get; private set; }

    public IReadOnlyCollection<MiniGameUiSession> Participants => _participants.Values;

    internal IMiniGameClient Client { get; set; } = null!;

    internal Action<Player>? Released { get; set; }

    public virtual bool Playable => true;

    public bool TryGetParticipant(Player player, out MiniGameUiSession participant)
        => _participants.TryGetValue(player.Guid, out participant!);

    internal void Open(Player player)
    {
        var participant = new MiniGameUiSession(Id, Definition, player);

        _participants[player.Guid] = participant;

        Client.ShowDetails(participant);
    }

    internal void RefreshDetails(Player player)
    {
        if (TryGetParticipant(player, out var participant) && participant.State == MiniGameUiState.Details)
            Client.RefreshDetails(participant);
    }

    internal bool Enter(Player player)
    {
        if (!TryGetParticipant(player, out var participant) || participant.State != MiniGameUiState.Details)
            return false;

        if (!Playable || State is MiniGameState.Ending or MiniGameState.Closed || !Host.TryEnter(player, out var zone))
        {
            Client.Close(participant);
            Remove(participant);
            return false;
        }

        participant.State = MiniGameUiState.Entering;

        OnEntered(player, zone);

        if (!Host.LoadsZone)
            Begin(participant);

        return true;
    }

    internal void ClientIsReady(Player player)
    {
        if (!TryGetParticipant(player, out var participant))
            return;

        if (participant.State == MiniGameUiState.Entering)
        {
            Begin(participant);
            return;
        }

        if (participant.State == MiniGameUiState.Leaving)
        {
            Client.Reset(player);
            Remove(participant);
        }
    }

    internal void Leave(Player player, MiniGameEndReason reason)
    {
        if (!TryGetParticipant(player, out var participant) || participant.State == MiniGameUiState.Leaving)
            return;

        if (participant.State == MiniGameUiState.Details)
        {
            Client.Close(participant);
            Remove(participant);
            return;
        }

        OnLeft(player, reason);

        // A disconnected player has no client to load back out.
        if (!Host.LoadsZone || reason == MiniGameEndReason.Disconnected)
        {
            if (reason != MiniGameEndReason.Disconnected)
                Client.Close(participant);

            Remove(participant);
            return;
        }

        participant.State = MiniGameUiState.Leaving;

        Client.Leave(participant);
        Host.Leave(player);
    }

    internal bool Finish(MiniGameResult result)
    {
        if (State != MiniGameState.Running)
            return false;

        State = MiniGameState.Ending;

        foreach (var participant in _participants.Values.Where(p => p.State == MiniGameUiState.Running))
            Client.ShowEndScreen(participant, result);

        OnFinished(result);

        return true;
    }

    internal void End()
    {
        if (State == MiniGameState.Closed)
            return;

        State = MiniGameState.Ending;

        foreach (var participant in _participants.Values.ToList())
            Leave(participant.Player, MiniGameEndReason.Ended);
    }

    private void Begin(MiniGameUiSession participant)
    {
        Client.Start(participant);

        if (State == MiniGameState.Open)
        {
            State = MiniGameState.Running;
            OnStarted();
        }

        OnPlayerStarted(participant.Player);
    }

    private void Remove(MiniGameUiSession participant)
    {
        _participants.Remove(participant.Player.Guid);

        Released?.Invoke(participant.Player);

        if (_participants.Count != 0 || State == MiniGameState.Closed)
            return;

        State = MiniGameState.Closed;
        OnClosed();
    }

    protected virtual void OnEntered(Player player, IZone zone)
    {
    }

    protected virtual void OnStarted()
    {
    }

    protected virtual void OnPlayerStarted(Player player)
    {
    }

    protected virtual void OnLeft(Player player, MiniGameEndReason reason)
    {
    }

    protected virtual void OnFinished(MiniGameResult result)
    {
    }

    protected virtual void OnClosed()
    {
    }
}
