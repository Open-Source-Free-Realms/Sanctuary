using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// 165/12 — sent when the player re-acquires a recently-sold item from the coin store's "Buy Back"
// tab. Recovered byte-for-byte from p12.pcap (tunnel op 5): inner = A5 00 0C 00 | int EntryId |
// int Unknown. The client waits for a 165/13 ack (see CoinStoreBuyBackResponsePacket) which echoes
// EntryId. Full RE: fr-re/findings/merchant-buyback-investigation.md.
public class CoinStoreBuyBackRequestPacket : BaseCoinStorePacket, IDeserializable<CoinStoreBuyBackRequestPacket>
{
    public new const short OpCode = 12;

    public int EntryId;   // capture: 3 — echoed back in the response
    public int Unknown;   // capture: 0

    public CoinStoreBuyBackRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CoinStoreBuyBackRequestPacket value)
    {
        value = new CoinStoreBuyBackRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.EntryId))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
