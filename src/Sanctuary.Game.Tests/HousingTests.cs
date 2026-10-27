using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class HousingTests
{
    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Houses.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    [TestMethod]
    public void EveryHouse_HasHousingZone()
    {
        var resourcesDirectory = GetResourcesDirectory();

        var houses = new HouseDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(houses.Load(Path.Combine(resourcesDirectory, "Houses.json")));

        var zones = new ZoneDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(zones.Load(Path.Combine(resourcesDirectory, "Zones")));

        foreach (var house in houses.Values)
        {
            Assert.IsTrue(zones.TryGetValue(house.ZoneId, out var zone), $"House {house.Id} has no zone {house.ZoneId}.");
            Assert.IsInstanceOfType<HousingZoneDefinition>(zone, $"Zone {house.ZoneId} is not a housing zone.");
            Assert.IsTrue(house.BuildAreas.Count > 0, $"House {house.Id} has no build areas.");
        }
    }

    [TestMethod]
    public void HouseItems_AreHouseDeeds()
    {
        var resourcesDirectory = GetResourcesDirectory();

        var houses = new HouseDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(houses.Load(Path.Combine(resourcesDirectory, "Houses.json")));

        using var stream = File.OpenRead(Path.Combine(resourcesDirectory, "ClientItemDefinitions.json"));
        using var document = JsonDocument.Parse(stream);

        var houseItemIds = document.RootElement.EnumerateArray()
            .Where(x => x.TryGetProperty("Type", out var type) && type.GetInt32() == 16)
            .Select(x => x.GetProperty("Id").GetInt32())
            .ToHashSet();

        var itemIds = houses.Values.SelectMany(x => x.ItemDefinitionIds).ToList();

        Assert.IsTrue(itemIds.Count > 0);
        CollectionAssert.AllItemsAreUnique(itemIds);

        foreach (var itemId in itemIds)
            Assert.IsTrue(houseItemIds.Contains(itemId), $"Item {itemId} is not a house item.");
    }

    private static byte[] GetHousingPacket(short subOpCode, Action<PacketWriter> write)
    {
        using var writer = new PacketWriter();

        writer.Write(BaseHousingPacket.OpCode);
        writer.Write(subOpCode);

        write(writer);

        return writer.Buffer;
    }

    [TestMethod]
    public void PlaceFixture_Deserializes()
    {
        var data = GetHousingPacket(ClientHousingPacketPlaceFixture.OpCode, writer =>
        {
            writer.Write(42);
            writer.Write(new Vector4(1.0f, 2.0f, 3.0f, 1.0f));
            writer.Write(new Quaternion(0.0f, 1.0f, 0.0f, 0.0f));
            writer.Write(1.5f);
        });

        Assert.IsTrue(ClientHousingPacketPlaceFixture.TryDeserialize(data, out var packet));
        Assert.AreEqual(42, packet.ItemGuid);
        Assert.AreEqual(new Vector4(1.0f, 2.0f, 3.0f, 1.0f), packet.Position);
        Assert.AreEqual(1.5f, packet.Scale);
    }

    [TestMethod]
    public void PlaceFixtureRequest_Deserializes()
    {
        var data = GetHousingPacket(ClientHousingPacketPlaceFixtureRequest.OpCode, writer =>
        {
            writer.Write(0);
            writer.Write(10472);
            writer.Write(3);
            writer.Write(true);
        });

        Assert.IsTrue(ClientHousingPacketPlaceFixtureRequest.TryDeserialize(data, out var packet));
        Assert.AreEqual(10472, packet.ItemDefinitionId);
        Assert.AreEqual(3, packet.TintId);
        Assert.IsTrue(packet.PlaceImmediately);
    }

    [TestMethod]
    public void SaveFixture_Deserializes()
    {
        var data = GetHousingPacket(ClientHousingPacketSaveFixture.OpCode, writer =>
        {
            writer.Write(0x123UL);
            writer.Write(new Vector4(1.0f, 2.0f, 3.0f, 1.0f));
            writer.Write(Quaternion.Identity);
            writer.Write(7);

            new CustomizationDetail
            {
                Type = 1,
                TextureAlias = "fun-coffinopen-L1",
                TintAlias = "dyetint",
                TintId = 7
            }.Serialize(writer);

            writer.Write(2.0f);
        });

        Assert.IsTrue(ClientHousingPacketSaveFixture.TryDeserialize(data, out var packet));
        Assert.AreEqual(0x123UL, packet.FixtureGuid);
        Assert.AreEqual(7, packet.TintId);
        Assert.AreEqual("dyetint", packet.Customization.TintAlias);
        Assert.AreEqual(2.0f, packet.Scale);
    }

    [TestMethod]
    public void HeaderOnlyPackets_RejectTrailingData()
    {
        var data = GetHousingPacket(ClientHousingPacketToggleLocked.OpCode, writer => writer.Write(1));

        Assert.IsFalse(ClientHousingPacketToggleLocked.TryDeserialize(data, out _));
    }

    [TestMethod]
    public void InstanceInfo_MatchesClientFieldOrder()
    {
        var info = new PlayerHousingInstanceInfo
        {
            IconId = 1,
            FixtureCount = 2,
            LastVisited = DateTimeOffset.FromUnixTimeSeconds(3),
            FurnitureScore = 4,
            FactoryPlotId = 5,
            Unknown21 = "x",
            WhenCreated = DateTimeOffset.FromUnixTimeSeconds(6)
        };

        using var writer = new PacketWriter();
        info.Serialize(writer);

        var reader = new PacketReader(writer.Buffer);

        // Client order: owner guid, instance guid, name id, house name, owner name,
        // icon, fixture count, last visited (8 bytes), furniture score, 3 flags,
        // description, keywords, rating, votes, 2 flags, factory plot, string, created.
        Assert.IsTrue(reader.TryRead(out ulong _));
        Assert.IsTrue(reader.TryRead(out ulong _));
        Assert.IsTrue(reader.TryRead(out int _));
        Assert.IsTrue(reader.TryRead(out string _));
        Assert.IsTrue(reader.TryRead(out string _));
        Assert.IsTrue(reader.TryRead(out int iconId));
        Assert.IsTrue(reader.TryRead(out int fixtureCount));
        Assert.IsTrue(reader.TryRead(out long lastVisited));
        Assert.IsTrue(reader.TryRead(out int furnitureScore));
        Assert.IsTrue(reader.TryRead(out bool _));
        Assert.IsTrue(reader.TryRead(out bool _));
        Assert.IsTrue(reader.TryRead(out bool _));
        Assert.IsTrue(reader.TryRead(out string _));
        Assert.IsTrue(reader.TryRead(out string _));
        Assert.IsTrue(reader.TryRead(out float _));
        Assert.IsTrue(reader.TryRead(out int _));
        Assert.IsTrue(reader.TryRead(out bool _));
        Assert.IsTrue(reader.TryRead(out bool _));
        Assert.IsTrue(reader.TryRead(out int factoryPlotId));
        Assert.IsTrue(reader.TryRead(out string unknown21));
        Assert.IsTrue(reader.TryRead(out long whenCreated));

        Assert.AreEqual(1, iconId);
        Assert.AreEqual(2, fixtureCount);
        Assert.AreEqual(3L, lastVisited);
        Assert.AreEqual(4, furnitureScore);
        Assert.AreEqual(5, factoryPlotId);
        Assert.AreEqual("x", unknown21);
        Assert.AreEqual(6L, whenCreated);
        Assert.AreEqual(0, reader.RemainingLength);
    }

    [TestMethod]
    public void FixtureAsset_StartsWithModelThenEchoedItem()
    {
        var data = new HousingPacketFixtureAsset
        {
            ModelId = 1234,
            ItemGuid = 56
        }.Serialize();

        var reader = new PacketReader(data);

        // The client builds the preview from the first int and echoes the second in PlaceFixture.
        Assert.IsTrue(reader.TryRead(out short _));
        Assert.IsTrue(reader.TryRead(out short _));
        Assert.IsTrue(reader.TryRead(out int modelId));
        Assert.IsTrue(reader.TryRead(out int itemGuid));

        Assert.AreEqual(1234, modelId);
        Assert.AreEqual(56, itemGuid);
    }

    [TestMethod]
    public void RequestName_RoundTrips()
    {
        var data = new ClientHousingPacketRequestName
        {
            Name = "Cozy Cottage",
            Accepted = true
        }.Serialize();

        Assert.IsTrue(ClientHousingPacketRequestName.TryDeserialize(data, out var packet));
        Assert.AreEqual("Cozy Cottage", packet.Name);
        Assert.IsTrue(packet.Accepted);
    }
}
