using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client "you earned an item" reward celebration (opcode 50 / sub-opcode 2). Sibling of
// RewardBundlePacket (50/1, coins/stars) in the reward-celebration dispatcher (FUN_00b8a640 case 2);
// case 2 deserializes via FUN_00b891f0 then shows a single item reward using the "RewardPatternOne"
// fly-in layout (display FUN_00b899f0, which looks the item up by definition id).
// Wire (FUN_00b891f0 read order): short OpCode(50) + byte SubOpCode(2) [3-byte header], then six
// int32s = 27 bytes total. Confirmed in-game:
//   +0x1c = ItemDefinitionId (the item shown, looked up in the client item-definition hash)
//   +0x30 = Quantity (the "received N" count in the popup)
// The remaining four ints (+0x20, the nested +0x28/+0x2c, +0x34) aren't needed for a simple item
// grant and are sent as 0 (likely tint / item guid / reward-context, unused here).
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
