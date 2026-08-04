using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

// op35/sub10 AddNotifications, byte-exact vs a real 2014 capture: Combat=true is the short 14-byte form (red crossed-swords badge); Combat=false is the full form used for other notification types (e.g. quest "!"/"?").
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
