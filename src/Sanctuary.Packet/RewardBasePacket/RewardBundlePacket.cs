using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RewardBundlePacket : RewardBasePacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    // The coin shop provided the original item-entry capture. The entry list and item
    // subtype shape were recovered from the client's reward bundle implementation.
    public RewardBundleBase RewardBundle { get; } = new();
    public int Unknown8;

    public RewardBundlePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        RewardBundle.Serialize(writer);
        writer.Write(Unknown8);

        return writer.Buffer;
    }
}
