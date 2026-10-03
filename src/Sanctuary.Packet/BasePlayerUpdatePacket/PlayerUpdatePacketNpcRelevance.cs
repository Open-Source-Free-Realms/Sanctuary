using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketNpcRelevance : BasePlayerUpdatePacket, ISerializablePacket, IDeserializable<PlayerUpdatePacketNpcRelevance>
{
    public new const short OpCode = 12;

    public class Entry : ISerializableType, IDeserializableType
    {
        public ulong Guid;

        public bool HasCursor;

        /// <summary>
        /// Id from Cursors.txt
        /// </summary>
        public byte CursorId;

        public bool Unknown2;

        public bool TryRead(ref PacketReader reader)
        {
            if (!reader.TryRead(out Guid) || !reader.TryRead(out HasCursor))
                return false;
            if (HasCursor && !reader.TryRead(out CursorId))
                return false;
            return reader.TryRead(out Unknown2);
        }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Guid);

            writer.Write(HasCursor);

            if (HasCursor)
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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerUpdatePacketNpcRelevance value)
    {
        value = new PlayerUpdatePacketNpcRelevance();
        var reader = new PacketReader(data);
        if (!value.TryRead(ref reader) || !reader.TryReadList(out value.Entries))
            return false;
        return reader.RemainingLength == 0;
    }

}