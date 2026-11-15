using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class IncomingRequestTests
{
    private const ulong RequesterGuid = 42;
    private const double ExpirySeconds = 310;

    private static Player CreatePlayer(TestZone zone)
    {
        var zoneManager = new ZoneManager(NullLoggerFactory.Instance, new ResourceManager(NullLogger<ResourceManager>.Instance),
            new ServiceCollection().BuildServiceProvider());

        return new Player(zone, null!, null!, zoneManager);
    }

    private static void Advance(Player player, TestZone zone, double seconds)
    {
        for (var elapsed = 0d; elapsed < seconds; elapsed += zone.TickDelta.TotalSeconds)
            player.Routines.OnTick();
    }

    [TestMethod]
    public void IncomingFriendRequest_ExpiresJustAfterFiveMinutes()
    {
        using var zone = TestZone.Create();
        var player = CreatePlayer(zone);

        Assert.IsTrue(player.TryAddIncomingFriendRequest(RequesterGuid));
        Assert.IsFalse(player.TryAddIncomingFriendRequest(RequesterGuid), "A second request from the same player is refused while one is pending.");

        Advance(player, zone, ExpirySeconds - 1);
        Assert.IsTrue(player.IncomingFriendRequests.Contains(RequesterGuid));

        Advance(player, zone, 2);
        Assert.IsFalse(player.IncomingFriendRequests.Contains(RequesterGuid));
        Assert.IsTrue(player.TryAddIncomingFriendRequest(RequesterGuid), "The player can be asked again once the old request has expired.");
    }

    [TestMethod]
    public void IncomingGuildInvite_ExpiresJustAfterFiveMinutes()
    {
        using var zone = TestZone.Create();
        var player = CreatePlayer(zone);

        Assert.IsTrue(player.TryAddIncomingGuildInvite(RequesterGuid));

        Advance(player, zone, ExpirySeconds - 1);
        Assert.IsTrue(player.IncomingGuildInvites.Contains(RequesterGuid));

        Advance(player, zone, 2);
        Assert.IsFalse(player.IncomingGuildInvites.Contains(RequesterGuid));
    }

    [TestMethod]
    public void AnsweredFriendRequest_DoesNotExpireALaterOne()
    {
        using var zone = TestZone.Create();
        var player = CreatePlayer(zone);

        player.TryAddIncomingFriendRequest(RequesterGuid);
        Advance(player, zone, ExpirySeconds - 10);

        Assert.IsTrue(player.TryRemoveIncomingFriendRequest(RequesterGuid));
        Assert.IsTrue(player.TryAddIncomingFriendRequest(RequesterGuid));

        Advance(player, zone, 20);
        Assert.IsTrue(player.IncomingFriendRequests.Contains(RequesterGuid), "The first request's timer must not clear the second request.");
    }
}
