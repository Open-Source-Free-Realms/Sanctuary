using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketUnequipSlot : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 6;

    public int Slot;
    public int ProfileId;

    public ClientUpdatePacketUnequipSlot() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Slot);
        writer.Write(ProfileId);

        return writer.Buffer;
    }
}