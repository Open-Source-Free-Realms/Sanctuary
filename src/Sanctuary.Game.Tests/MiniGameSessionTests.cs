using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.MiniGames.Testing;
using Sanctuary.Game.Zones;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MiniGameSessionTests
{
    private sealed class RecordingClient : IMiniGameClient
    {
        public List<string> Calls { get; } = [];

        public void ShowDetails(MiniGameUiSession participant) => Calls.Add("ShowDetails");
        public void RefreshDetails(MiniGameUiSession participant) => Calls.Add("RefreshDetails");
        public void Start(MiniGameUiSession participant) { participant.State = MiniGameUiState.Running; Calls.Add("Start"); }
        public void ShowEndScreen(MiniGameUiSession participant, MiniGameResult result) { participant.State = MiniGameUiState.Ended; Calls.Add($"EndScreen {result.EarnedStars}"); }
        public void Leave(MiniGameUiSession participant) => Calls.Add("Leave");
        public void Close(MiniGameUiSession participant) => Calls.Add("Close");
        public void Reset(Player player) => Calls.Add("Reset");
        public void ProfileSwitched(Player player, ClientPcProfile profile, int messageId) => Calls.Add("ProfileSwitched");
        public void Unavailable(Player player) => Calls.Add("Unavailable");
    }

    private sealed class FakeZoneHost(IZone zone) : IMiniGameHost
    {
        public bool LoadsZone => true;
        public int Left { get; private set; }

        public bool TryEnter(Player player, [MaybeNullWhen(false)] out IZone entered)
        {
            entered = zone;
            return true;
        }

        public void Leave(Player player) => Left++;
    }

    private sealed class ZoneSession(MiniGameUiDefinition definition, IMiniGameHost host) : MiniGameSession(definition, host)
    {
        public List<string> Hooks { get; } = [];
        protected override void OnStarted() => Hooks.Add("Started");
        protected override void OnLeft(Player player, MiniGameEndReason reason) => Hooks.Add($"Left {reason}");
        protected override void OnClosed() => Hooks.Add("Closed");
    }

    private sealed class OwnClientSession : MiniGameSession
    {
        public OwnClientSession(MiniGameUiDefinition definition, IMiniGameClient client)
            : base(definition, WorldMiniGameHost.Instance)
        {
            UseClient(client);
        }
    }

    private static ulong _nextGuid = 1;

    private static (MiniGameManager Manager, RecordingClient Client) CreateManager()
    {
        var client = new RecordingClient();
        return (new MiniGameManager(null!, [], client), client);
    }

    private static Player CreatePlayer(BaseZone zone) => new(zone, null!, null!, null!) { Guid = _nextGuid++ };

    private static MiniGameUiDefinition Definition() => new() { GameId = 42 };

    [TestMethod]
    public void WorldGame_StartsOnPlay_FinishesAndReleasesSlotOnEndScreenClose()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        Assert.IsTrue(manager.Open(player, session));
        Assert.AreSame(session, player.MiniGame);
        Assert.AreEqual(MiniGameState.Open, session.State);

        Assert.IsTrue(manager.Enter(player, session.Id));
        Assert.AreEqual(MiniGameState.Running, session.State);

        Assert.IsTrue(manager.Finish(player, new MiniGameResult { EarnedStars = 2 }));
        Assert.AreEqual(MiniGameState.Ending, session.State);
        Assert.IsFalse(manager.Finish(player, new MiniGameResult()), "A game finishes once.");

        Assert.IsTrue(manager.Leave(player));
        Assert.IsNull(player.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);

        CollectionAssert.AreEqual(new[] { "ShowDetails", "Start", "EndScreen 2", "Close" }, client.Calls);
        Assert.IsTrue(session.Events.Any(e => e.EndsWith("(Finished)")));
    }

    [TestMethod]
    public void SessionWithOwnClient_SendsThroughItInsteadOfTheDefault()
    {
        using var zone = TestZone.Create();
        var (manager, defaultClient) = CreateManager();
        var ownClient = new RecordingClient();
        var player = CreatePlayer(zone);
        var session = new OwnClientSession(Definition(), ownClient);

        manager.Open(player, session);
        manager.Enter(player, session.Id);

        CollectionAssert.AreEqual(new[] { "ShowDetails", "Start" }, ownClient.Calls);
        Assert.AreEqual(0, defaultClient.Calls.Count);
    }

    [TestMethod]
    public void Abort_DuringRun_ClosesAndReleases()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);
        manager.Enter(player, session.Id);

        Assert.IsTrue(manager.Leave(player));
        Assert.IsNull(player.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);
        Assert.IsTrue(session.Events.Any(e => e.EndsWith("(Aborted)")));
    }

    [TestMethod]
    public void CancelDetails_ClosesWithoutStarting()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);
        manager.Leave(player);

        Assert.IsNull(player.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);
        CollectionAssert.AreEqual(new[] { "ShowDetails", "Close" }, client.Calls);
        CollectionAssert.DoesNotContain((ICollection)session.Events, "Started");
    }

    [TestMethod]
    public void End_StopsGameForEveryParticipant()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var first = CreatePlayer(zone);
        var second = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(first, session);
        manager.Open(second, session);
        manager.Enter(first, session.Id);
        manager.Enter(second, session.Id);

        Assert.IsTrue(manager.End(first));

        Assert.IsNull(first.MiniGame);
        Assert.IsNull(second.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);
        Assert.AreEqual(1, session.Events.Count(e => e == "Started"), "The game starts once for all players.");
        Assert.AreEqual(2, session.Events.Count(e => e.EndsWith("(Ended)")));
    }

    [TestMethod]
    public void OnePlayerLeaving_KeepsGameRunningForOthers()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var first = CreatePlayer(zone);
        var second = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(first, session);
        manager.Open(second, session);
        manager.Enter(first, session.Id);
        manager.Enter(second, session.Id);

        manager.Leave(first);

        Assert.IsNull(first.MiniGame);
        Assert.AreEqual(MiniGameState.Running, session.State);
        Assert.AreSame(session, second.MiniGame);
        Assert.AreEqual(1, session.Participants.Count);
    }

    [TestMethod]
    public void SecondGame_IsRefusedWhileInOne()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);

        Assert.IsFalse(manager.Open(player, new TestMiniGameSession(Definition())));
        Assert.AreSame(session, player.MiniGame);
    }

    [TestMethod]
    public void Enter_WithWrongSessionId_IsIgnored()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);

        Assert.IsFalse(manager.Enter(player, session.Id + 1000));
        Assert.AreEqual(MiniGameState.Open, session.State);
    }

    [TestMethod]
    public void UnavailableGame_ReportsAndReleasesOnPlay()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new UnavailableMiniGameSession(Definition());

        manager.Open(player, session);

        Assert.IsFalse(manager.Enter(player, session.Id));
        Assert.IsNull(player.MiniGame);
        CollectionAssert.AreEqual(new[] { "ShowDetails", "Close", "Unavailable" }, client.Calls);
    }

    [TestMethod]
    public void Disconnect_ReleasesWithoutSendingToClient()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new ZoneSession(Definition(), new FakeZoneHost(zone));

        manager.Open(player, session);
        manager.Enter(player, session.Id);
        manager.ClientIsReady(player);
        client.Calls.Clear();

        manager.Disconnect(player);

        Assert.IsNull(player.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);
        Assert.AreEqual(0, client.Calls.Count);
        CollectionAssert.Contains(session.Hooks, "Left Disconnected");
    }

    [TestMethod]
    public void OnlyGamesWithASession_GetAPlayablePlayButton()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var builtPlayer = CreatePlayer(zone);
        var unbuiltPlayer = CreatePlayer(zone);
        var built = new TestMiniGameSession(Definition());
        var unbuilt = new UnavailableMiniGameSession(Definition());

        manager.Open(builtPlayer, built);
        manager.Open(unbuiltPlayer, unbuilt);

        Assert.IsTrue(built.TryGetParticipant(builtPlayer, out var builtParticipant) && builtParticipant.Playable);
        Assert.IsTrue(unbuilt.TryGetParticipant(unbuiltPlayer, out var unbuiltParticipant) && !unbuiltParticipant.Playable);
    }

    [TestMethod]
    public void CancelDetails_IsIgnoredOnceTheGameIsRunning()
    {
        using var zone = TestZone.Create();
        var (manager, _) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);
        manager.Enter(player, session.Id);

        Assert.IsFalse(manager.CancelDetails(player));
        Assert.AreSame(session, player.MiniGame);
        Assert.AreEqual(MiniGameState.Running, session.State);
    }

    [TestMethod]
    public void CloseEndScreen_IsIgnoredOnTheDetailsScreen()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var session = new TestMiniGameSession(Definition());

        manager.Open(player, session);

        Assert.IsFalse(manager.CloseEndScreen(player));
        Assert.AreSame(session, player.MiniGame);
        CollectionAssert.AreEqual(new[] { "ShowDetails" }, client.Calls);
    }

    [TestMethod]
    public void ZoneGame_StartsWhenClientIsReady_AndReleasesAfterLoadingBack()
    {
        using var zone = TestZone.Create();
        var (manager, client) = CreateManager();
        var player = CreatePlayer(zone);
        var host = new FakeZoneHost(zone);
        var session = new ZoneSession(Definition(), host);

        manager.Open(player, session);
        manager.Enter(player, session.Id);

        Assert.AreEqual(MiniGameState.Open, session.State, "A zone game waits for the zone to load.");

        manager.ClientIsReady(player);
        Assert.AreEqual(MiniGameState.Running, session.State);

        manager.Leave(player);
        Assert.AreEqual(1, host.Left);
        Assert.AreSame(session, player.MiniGame, "The slot stays held until the player has loaded back.");

        manager.ClientIsReady(player);
        Assert.IsNull(player.MiniGame);
        Assert.AreEqual(MiniGameState.Closed, session.State);

        CollectionAssert.AreEqual(new[] { "ShowDetails", "Start", "Leave", "Reset" }, client.Calls);
    }
}
