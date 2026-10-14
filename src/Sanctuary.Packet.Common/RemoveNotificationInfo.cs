using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class RemoveNotificationInfo : ISerializableType
{
    public ulong Guid { get; set; }

    public int ThoughtBubbleIconId { get; set; }

    public int CompositeEffectId { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(ThoughtBubbleIconId);
        writer.Write(CompositeEffectId);
    }
}
