using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class NotificationInfo : ISerializableType
{
    public ulong Guid { get; set; }

    public int Unknown3 { get; set; }
    public int DescriptionId { get; set; }
    public int ImageId { get; set; }
    public int NameId { get; set; }
    public int SubTextId { get; set; }
    public int Type { get; set; }
    public bool Unknown8 { get; set; }
    public int CompositeEffectId { get; set; }
    public bool Combat { get; set; }
    public bool Unknown10 { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(Combat);
        writer.Write(Type);

        if (!Combat)
        {
            writer.Write(Unknown3);
            writer.Write(ImageId);
            writer.Write(DescriptionId);
            writer.Write(NameId);
            writer.Write(SubTextId);
            writer.Write(Unknown8);
            writer.Write(CompositeEffectId);
        }

        writer.Write(Unknown10);
    }
}