using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client "you earned an item" reward celebration (opcode 50 / sub 2), sibling of RewardBundlePacket (50/1). Wire order from FUN_00b891f0: header + six int32s; only +0x1c (ItemDefinitionId) and +0x30 (Quantity) are used, the rest are sent as 0.
public class RewardNonBundledItemPacket : ISerializablePacket
{
    public const short OpCode = 50;
    public const byte SubOpCode = 2;

    public int ItemDefinitionId; // +0x1c - the item to show
    public int Unknown20;        // +0x20
    public int Unknown28;        // +0x28 (nested pair, first int)
    public int Unknown2c;        // +0x2c (nested pair, second int)
    public int Quantity;         // +0x30 - the "received N" count
    public int Unknown34;        // +0x34

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);    // short (50)
        writer.Write(SubOpCode); // byte (2)

        writer.Write(ItemDefinitionId);
        writer.Write(Unknown20);
        writer.Write(Unknown28);
        writer.Write(Unknown2c);
        writer.Write(Quantity);
        writer.Write(Unknown34);

        return writer.Buffer;
    }
}
