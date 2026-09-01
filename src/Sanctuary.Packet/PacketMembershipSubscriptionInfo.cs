using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMembershipSubscriptionInfo : ISerializablePacket
{
    public const short OpCode = 189;

    public bool IsMember;
    public bool IsReferee;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(IsMember);
        writer.Write(IsReferee);

        return writer.Buffer;
    }
}