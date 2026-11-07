using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class PlayerTeleportTests
{
    private const string TeleportPendingField = "_teleportPending";

    private static void SetTeleportPending(Player player)
    {
        var teleportPendingField = typeof(Player).GetField(TeleportPendingField, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(teleportPendingField);

        teleportPendingField.SetValue(player, true);
    }

    [TestMethod]
    public void TryAcceptTeleportSequence_RejectsStaleSequenceWhileTeleportIsPending()
    {
        using var zone = TestZone.Create();

        var player = new Player(zone, null!, null!, null!);
        var staleSequence = player.TeleportSequence;
        var currentSequence = player.NextTeleportSequence();
        SetTeleportPending(player);

        Assert.IsFalse(player.TryAcceptTeleportSequence(staleSequence));
        Assert.IsFalse(player.TryAcceptTeleportSequence(staleSequence));
        Assert.AreEqual(currentSequence, player.TeleportSequence);
    }

    [TestMethod]
    public void TryAcceptTeleportSequence_CurrentSequenceFinishesTheTeleport()
    {
        using var zone = TestZone.Create();

        var player = new Player(zone, null!, null!, null!);
        var staleSequence = player.TeleportSequence;
        var currentSequence = player.NextTeleportSequence();
        SetTeleportPending(player);

        Assert.IsTrue(player.TryAcceptTeleportSequence(currentSequence));
        Assert.IsTrue(player.TryAcceptTeleportSequence(staleSequence));
        Assert.AreEqual(staleSequence, player.TeleportSequence);
    }
}
