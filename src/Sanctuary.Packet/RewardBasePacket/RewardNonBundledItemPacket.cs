using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RewardNonBundledItemPacket : RewardBasePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int ItemDefinitionId;
    public int Unknown20;
    public int Unknown28;
    public int Unknown2c;
    public int Quantity;
    public int Unknown34;

    public RewardNonBundledItemPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ItemDefinitionId);
        writer.Write(Unknown20);
        writer.Write(Unknown28);
        writer.Write(Unknown2c);
        writer.Write(Quantity);
        writer.Write(Unknown34);

        return writer.Buffer;
    }
}
