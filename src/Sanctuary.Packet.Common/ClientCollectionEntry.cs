using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class ClientCollectionEntry : ISerializableType
{
    public int Id;
    public int DefinitionId;
    public int Index;
    public int CategoryId;
    public int NameId;
    public int IconId;
    public int IconTintId;
    public int Unknown;
    public bool Collected;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(DefinitionId);
        writer.Write(Index);
        writer.Write(CategoryId);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(IconTintId);
        writer.Write(Unknown);
        writer.Write(Collected);
    }
}
