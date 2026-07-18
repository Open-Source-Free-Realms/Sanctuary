using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Tests;

[TestClass]
public sealed class ClientCollectionTests
{
    private const string CapturedBriarwoodRow =
        "0A0000009E4200009F420000030000004C0800005C00000001000000010900000000000000000000000F000000000000" +
        "00000000000000803F0000000000000000000000000000000091CA73956ACC524B3D0F00007009000001000000030000" +
        "00003D0F00000000000070090000320000000B00000000000000000000000000000000E4150000080000002900000029" +
        "000000010000000A000000A04200004C0800006300000000000000012A0000002A000000020000000A000000A1420000" +
        "4C0800006000000000000000002B0000002B000000030000000A000000A24200004C0800007000000000000000012C00" +
        "00002C000000040000000A000000A34200004C0800006400000000000000002D0000002D000000050000000A000000A4" +
        "4200004C0800005C00000000000000012E0000002E000000060000000A000000A54200004C0800006800000000000000" +
        "002F0000002F000000070000000A000000A64200004C0800006D00000000000000013000000030000000080000000A00" +
        "0000A74200004C080000650000000000000000";

    [TestMethod]
    public void Serialize_MatchesCapturedBriarwoodRow()
    {
        var collection = new ClientCollection
        {
            CategoryId = 10,
            Id = 17054,
            DescriptionId = 17055,
            Type = 3,
            IconId = 2124,
            IconTintId = 92,
            HeaderMetadata = 9,
            PlayerGuid = 0x4B52CC6A9573CA91,
            RewardMetadata = 11,
            Entries =
            [
                Entry(41, 1, 17056, 99, true),
                Entry(42, 2, 17057, 96, false),
                Entry(43, 3, 17058, 112, true),
                Entry(44, 4, 17059, 100, false),
                Entry(45, 5, 17060, 92, true),
                Entry(46, 6, 17061, 104, false),
                Entry(47, 7, 17062, 109, true),
                Entry(48, 8, 17063, 101, false)
            ]
        };

        using var writer = new PacketWriter();
        collection.Serialize(writer);

        CollectionAssert.AreEqual(Convert.FromHexString(CapturedBriarwoodRow), writer.Buffer);
    }

    private static ClientCollectionEntry Entry(int id, int index, int nameId, int tintId, bool collected)
    {
        return new ClientCollectionEntry
        {
            Id = id,
            DefinitionId = id,
            Index = index,
            CategoryId = 10,
            NameId = nameId,
            IconId = 2124,
            IconTintId = tintId,
            Collected = collected
        };
    }
}
