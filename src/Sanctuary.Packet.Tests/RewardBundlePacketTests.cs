using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Packet.Tests;

[TestClass]
public sealed class RewardBundlePacketTests
{
    [TestMethod]
    public void Serialize_WritesRewardAndEntryMetadata()
    {
        var packet = new RewardBundlePacket
        {
            SourceGuid = 0x0807060504030201,
            PlayerGuid = 0x1817161514131211,
            IconId = 0x21222324,
            NameId = 0x31323334,
            EntryIconId = 0x21222324,
            EntryNameId = 0x31323334,
            ItemDefinitionId = 0x41424344,
            Tint = 0x51525354,
            ItemGuid = 0x61626364,
            EntryUnknown5 = 0x71727374
        };

        Assert.AreEqual(
            "320001010000000000000000000000000300000000000000000000000000803F0000000000000000" +
            "0102030405060708111213141516171824232221343332310100000001000000002423222100000000" +
            "343332310100000044434241545352510000000000000000006463626174737271",
            Convert.ToHexString(packet.Serialize()));
    }
}
