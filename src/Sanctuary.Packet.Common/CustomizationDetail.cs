using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CustomizationDetail : ISerializableType
{
    // 1 - TextureAlias + TintAlias + TintId
    // 2 - TextureAlias + TintAlias + TintId + TextureOverride
    // 3 - TextureOverride
    public int Type;

    public string? TextureAlias;

    public string? TintAlias;
    public int TintId;

    public string? TextureOverride;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Type);

        writer.Write(TextureAlias);
        writer.Write(TintAlias);

        writer.Write(TintId);

        writer.Write(TextureOverride);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Type))
            return false;

        if (!reader.TryRead(out TextureAlias))
            return false;

        if (!reader.TryRead(out TintAlias))
            return false;

        if (!reader.TryRead(out TintId))
            return false;

        if (!reader.TryRead(out TextureOverride))
            return false;

        return true;
    }
}