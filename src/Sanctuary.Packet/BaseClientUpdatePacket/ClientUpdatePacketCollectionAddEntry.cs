using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class ClientUpdatePacketCollectionAddEntry : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 10;

    public int DefinitionId;
    public int IconId;
    public int IconTintId;
    public int NameId;
    public int CollectionId;
    public int Index;
    public int Unknown;
    public bool Collected = true;

    public ClientUpdatePacketCollectionAddEntry() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(DefinitionId);
        writer.Write(IconId);
        writer.Write(IconTintId);
        writer.Write(NameId);
        writer.Write(CollectionId);
        writer.Write(Index);
        writer.Write(Unknown);
        writer.Write(Collected);

        return writer.Buffer;
    }
}
