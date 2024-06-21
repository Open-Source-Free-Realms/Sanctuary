using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClaimCodeInfo : ISerializableType
{
    public string Code = null!;

    public int NameId;
    public int DescriptionId;

    public int IconId;

    public string TintAlias = null!;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Code);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(IconId);

        writer.Write(TintAlias);
    }
}