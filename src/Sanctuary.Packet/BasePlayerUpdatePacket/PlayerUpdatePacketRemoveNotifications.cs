using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRemoveNotifications : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 11;

    public class Entry : ISerializableType
    {
        public ulong Guid;

        public int ThoughtBubbleIconId;

        public int CompositeEffectId;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Guid);

            writer.Write(ThoughtBubbleIconId);

            writer.Write(CompositeEffectId);
        }
    }

    public List<Entry> Entries = [];

    public PlayerUpdatePacketRemoveNotifications() : base(OpCode)
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
