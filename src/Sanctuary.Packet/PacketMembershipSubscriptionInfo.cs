using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMembershipSubscriptionInfo : ISerializablePacket
{
    public const short OpCode = 189;

    // Membership no longer exists. The client still locks member content behind this flag, so everyone is a member.
    public bool IsMember = true;
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