using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CharacterAttachmentData : ISerializableType
{
    public string ModelName = null!;
    public string TextureAlias = null!;
    public string TintAlias = null!;
    public int TintId;
    public int CompositeEffectId;
    public int Slot;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ModelName);
        writer.Write(TextureAlias);
        writer.Write(TintAlias);
        writer.Write(TintId);
        writer.Write(CompositeEffectId);
        writer.Write(Slot);
    }
}