using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// BasePlayerUpdatePacket op35/sub35 shows a floating combat damage/heal number.
// Wire format from client UnserializePacket sub_8D6C50: 30 bytes total.
//   ulong Guid   (m_llGuid)   target
//   ulong Guid2  (m_llGuid2)  source / attacker
//   bool  Unknown  (m_bUnknown)
//   int   Unknown2 (m_nUnknown2)
//   int   Unknown3 (m_nUnknown3)
//   int   Unknown4 (m_nUnknown4)
//   bool  Unknown5 (m_bUnknown5)
// The bool fields and trailing ints still need live verification.
public class PlayerUpdatePacketHitPointModification : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 35;

    public ulong Guid;
    public ulong Guid2;

    public bool Unknown;

    public int Unknown2;
    public int Unknown3;
    public int Unknown4;

    public bool Unknown5;

    public PlayerUpdatePacketHitPointModification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // [op 35][sub 35]

        writer.Write(Guid);
        writer.Write(Guid2);

        writer.Write(Unknown);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        writer.Write(Unknown5);

        return writer.Buffer;
    }
}
