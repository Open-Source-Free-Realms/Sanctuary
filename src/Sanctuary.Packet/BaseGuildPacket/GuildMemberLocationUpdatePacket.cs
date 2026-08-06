using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildMemberLocationUpdatePacket : BaseGuildPacket, ISerializablePacket
{
    public new const short OpCode = 25;

    public ulong GuildGuid;

    public class Entry : ISerializableType
    {
        public ulong Guid;

        public bool InEncounter;

        public float LocationX;
        public float LocationZ;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Guid);

            writer.Write(InEncounter);

            writer.Write(LocationX);
            writer.Write(LocationZ);
        }
    }

    public List<Entry> Entries = new();

    public GuildMemberLocationUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(GuildGuid);

        writer.Write(Entries);

        return writer.Buffer;
    }
}
