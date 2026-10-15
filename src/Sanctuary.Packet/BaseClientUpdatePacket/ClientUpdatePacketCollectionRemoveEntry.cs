using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class ClientUpdatePacketCollectionRemoveEntry : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 11;

    public int CollectionId;
    public int EntryId;

    public ClientUpdatePacketCollectionRemoveEntry() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(CollectionId);
        writer.Write(EntryId);

        return writer.Buffer;
    }
}
