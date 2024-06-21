using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketNpcRelevance : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 12;

    public class Entry : ISerializableType
    {
        public ulong Guid;

        public bool Unknown;

        /// <summary>
        /// Id from Cursors.txt
        /// </summary>
        public byte CursorId;

        public bool Unknown2;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Guid);

            writer.Write(Unknown);
            writer.Write(CursorId);
            writer.Write(Unknown2);
        }
    }

    public List<Entry> Entries = new();

    public PlayerUpdatePacketNpcRelevance() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Entries);

        return writer.Buffer;
    }
}