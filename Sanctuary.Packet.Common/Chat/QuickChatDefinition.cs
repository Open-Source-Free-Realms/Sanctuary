using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Chat;

public class QuickChatDefinition : ISerializableType
{
    public int Id { get; set; }
    public int MenuText { get; set; }
    public int ChatText { get; set; }
    public int AnimationId { get; set; }
    public int ParentId { get; set; }
    public int AdminOnly { get; set; }
    public int MenuIconId { get; set; }
    public int ItemId { get; set; }
    public int ItemCategory { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(MenuText);
        writer.Write(ChatText);
        writer.Write(AnimationId);
        writer.Write(ParentId);
        writer.Write(AdminOnly);
        writer.Write(MenuIconId);
        writer.Write(ItemId);
        writer.Write(ItemCategory);
    }
}