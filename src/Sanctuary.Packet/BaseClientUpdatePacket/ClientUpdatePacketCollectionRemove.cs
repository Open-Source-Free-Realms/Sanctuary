using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class ClientUpdatePacketCollectionRemove : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public int CollectionId;

    public ClientUpdatePacketCollectionRemove() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(CollectionId);

        return writer.Buffer;
    }
}
