using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: lifetime "quests completed" counter (op49 sub 12). Unverified in-game.
public class CompletedQuestCountUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 12;

    // Total number of quests this character has ever completed.
    public int Count;

    public CompletedQuestCountUpdatePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(12)

        writer.Write(Count);

        return writer.Buffer;
    }
}
