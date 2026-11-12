using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketImmediateActivationCheck : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 30;

    public bool Success;
     public int ItemGuid;
    public int CategoryId;
    public int BundleId;
    public int TitleStringId;
    public int BodyStringId;

    public ClientUpdatePacketImmediateActivationCheck() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ItemGuid);
        writer.Write(CategoryId);
        writer.Write(BundleId);
        writer.Write(TitleStringId);
        writer.Write(BodyStringId);
        writer.Write(Success);

        return writer.Buffer;
    }
}