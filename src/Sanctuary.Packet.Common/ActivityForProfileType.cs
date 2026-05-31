using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ActivityForProfileType : ISerializableType
{
    public int ProfileId;

    public int QuestId;

    public int IconId;

    public int BadgeId;

    public int QuestTitle;
    public int QuestDescription;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ProfileId);

        writer.Write(QuestId);

        writer.Write(IconId);

        writer.Write(BadgeId);

        writer.Write(QuestTitle);
        writer.Write(QuestDescription);
    }
}