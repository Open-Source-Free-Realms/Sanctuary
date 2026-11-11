using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TradeInventoryPublicationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicationUsesValidatedDefinitionsWhenCatalogEntryDisappears(bool existingStack)
    {
        using var zone = TestZone.Create();
        var resources = new ResourceManager(NullLogger<ResourceManager>.Instance);
        var definition = new ClientItemDefinition { Id = 100, NameId = 123, Icon = new() { Id = 456 } };
        var player = new Player(zone, null!, resources, null!);
        if (existingStack)
        {
            player.Items.Add(new ClientItem { Id = 7, Definition = 100, Count = 5 });
            player.ActionBarItemGuids[2] = new() { [0] = 7 };
        }
        // Deliberately leave the current resource catalog empty after validation.
        var result = new TradeParticipantResult([new(7, 100, 0, 2)], 90,
            new Dictionary<int, ClientItemDefinition> { [100] = definition });
        var packets = ((ITradeParticipant)player).ApplyTradeResult(result);
        Assert.AreEqual(2, player.Items.Single().Count);
        Assert.AreEqual(90, player.Coins);
        if (existingStack)
            Assert.AreEqual(2, packets.OfType<ClientUpdatePacketUpdateActionBarSlot>().Single().Slot.Quantity);
        else
            Assert.AreEqual(1, packets.OfType<ClientUpdatePacketItemAdd>().Count());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void LiveAvailabilityReservesPersistedPetEquipmentOncePerPet(int slot)
    {
        using var zone = TestZone.Create();
        var resources = new ResourceManager(NullLogger<ResourceManager>.Instance);
        resources.ClientItemDefinitions.TryAdd(100, new ClientItemDefinition { Id = 100 });
        var player = new Player(zone, null!, resources, null!);
        player.Items.Add(new ClientItem { Id = 7, Definition = 100, Count = 2 });
        var pet = new PetClientInfo { Id = 3 };
        switch (slot)
        {
            case -1: pet.ItemGuids.Add(7); break;
            case 0: pet.ShampooItemGuid = 7; break;
            case 1: pet.ConditionerItemGuid = 7; break;
            case 2: pet.ToyItemGuid = 7; break;
            case 3: pet.BrushItemGuid = 7; break;
            case 4: pet.FoodBowlItemGuid = 7; break;
            case 5: pet.FoodItemGuid = 7; break;
            case 6: pet.VitaminItemGuid = 7; break;
            case 7: pet.UnknownItemGuid1 = 7; break;
            case 8: pet.ItemGuids.Add(7); pet.ShampooItemGuid = pet.ToyItemGuid = 7; break;
        }
        player.Pets.Add(pet);
        Assert.IsTrue(((ITradeParticipant)player).TryGetTradeItem(7, out _, out _, out var available));
        Assert.AreEqual(slot == 7 ? 2 : 1, available);
    }

    [TestMethod]
    [DataRow(2, false, 0)]
    [DataRow(7, false, 77)]
    [DataRow(2, true, 77)]
    [DataRow(7, true, 0)]
    public void StackChangesRefreshEveryMappedSlotAndPreserveCooldown(int count, bool coolingDown, int tint)
    {
        using var zone = TestZone.Create();
        var resources = new ResourceManager(NullLogger<ResourceManager>.Instance);
        resources.ClientItemDefinitions.TryAdd(100, new ClientItemDefinition
        {
            Id = 100, NameId = 123, Icon = new() { Id = 456, TintId = 33 }
        });
        var player = new Player(zone, null!, resources, null!);
        player.Items.Add(new ClientItem { Id = 7, Definition = 100, Count = 5, Tint = tint });
        player.ActionBarItemGuids[2] = new() { [0] = 7, [1] = 7, [2] = 8 };
        player.ActionBars[2] = new ClientActionBar
        {
            Slots = new() { [0] = new() { Quantity = 5 }, [1] = new() { Quantity = 5 }, [2] = new() { Quantity = 9 } }
        };
        var pending = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = 0 } };
        pending.Slot.Quantity = 5;
        pending.Slot.Enabled = true;
        pending.Slot.TotalRefreshTime = 60000;
        if (coolingDown)
        {
            player.StartItemCooldown(100, 60000);
            player.ScheduleSlotPacket(2, 0, pending, 60000);
        }

        var packets = ((ITradeParticipant)player).ApplyTradeResult(new([new(7, 100, tint, count)], 100,
            new System.Collections.Generic.Dictionary<int, ClientItemDefinition> { [100] = resources.ClientItemDefinitions[100] }));

        Assert.AreEqual(count, player.Items.Single().Count);
        Assert.AreEqual(count, packets.OfType<ClientUpdatePacketItemUpdate>().Single().Count);
        var updates = packets.OfType<ClientUpdatePacketUpdateActionBarSlot>().ToArray();
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, updates.Select(x => x.Data.Slot).ToArray());
        foreach (var update in updates)
        {
            Assert.AreEqual(count, update.Slot.Quantity);
            Assert.AreEqual(!coolingDown, update.Slot.Enabled);
            Assert.IsFalse(update.Slot.IsEmpty);
            Assert.AreEqual(456, update.Slot.IconId);
            Assert.AreEqual(tint == 0 ? 33 : tint, update.Slot.IconTintId);
            Assert.AreEqual(123, update.Slot.NameId);
            Assert.AreEqual(coolingDown ? 60000 : 1000, update.Slot.TotalRefreshTime);
            Assert.AreSame(update.Slot, player.ActionBars[2].Slots[update.Data.Slot]);
        }
        Assert.AreEqual(9, player.ActionBars[2].Slots[2].Quantity);
        Assert.AreEqual(8, player.ActionBarItemGuids[2][2]);
        if (coolingDown)
        {
            Assert.AreEqual(count, pending.Slot.Quantity);
            Assert.IsTrue(pending.Slot.Enabled);
            Assert.AreEqual(60000, pending.Slot.TotalRefreshTime);
            Assert.IsTrue(player.IsItemOnCooldown(100));
        }
    }
}
