using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketFixtureAsset : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 42;

    public int ModelDefinitionId;
    public int ItemDefinitionId;
    public FixtureDefinition Definition = new();
    public string TextureAlias = string.Empty;
    public string TintAlias = string.Empty;
    public int TintId;
    public int PreviewTintId;
    public string TextureOverride = string.Empty;
    public List<ulong> RelatedGuids = [];
    public int Unknown1;
    public int Unknown2;
    public bool IsPreview;

    public HousingPacketFixtureAsset() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ModelDefinitionId);
        writer.Write(ItemDefinitionId);
        Definition.Serialize(writer);
        writer.Write(TextureAlias);
        writer.Write(TintAlias);
        writer.Write(TintId);
        writer.Write(PreviewTintId);
        writer.Write(TextureOverride);
        writer.Write(RelatedGuids);
        writer.Write(Unknown1);
        writer.Write(Unknown2);
        writer.Write(IsPreview);

        return writer.Buffer;
    }
}
