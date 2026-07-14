using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// 165/13 - the ack the client waits for after a 165/12 Buy Back request. In p12.pcap the real
// server replied with a tiny 3-int packet: inner = A5 00 0D 00 | int EntryId (echoes the request) |
// int Result=1 | int Quantity=1. It is a standalone acknowledgement - the capture shows NO item/coin
// update bundled with it.
public class CoinStoreBuyBackResponsePacket : BaseCoinStorePacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public int EntryId;       // echo of the request's EntryId
    public int Result = 1;    // 1 = success (constant in capture)
    public int Quantity = 1;  // constant in capture

    public CoinStoreBuyBackResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);           // A5 00 0D 00
        writer.Write(EntryId);
        writer.Write(Result);
        writer.Write(Quantity);

        return writer.Buffer;
    }
}
