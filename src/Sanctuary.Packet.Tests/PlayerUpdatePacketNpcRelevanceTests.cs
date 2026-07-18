using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Packet.Tests;

[TestClass]
public sealed class PlayerUpdatePacketNpcRelevanceTests
{
    [TestMethod]
    public void Serialize_MatchesCapturedCursorEntry()
    {
        var packet = new PlayerUpdatePacketNpcRelevance();
        packet.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
        {
            Guid = 0x0000465000000022,
            HasCursor = true,
            CursorId = 20,
            Unknown2 = false
        });

        CollectionAssert.AreEqual(
            Convert.FromHexString("23000C00010000002200000050460000011400"),
            packet.Serialize());
    }
}
