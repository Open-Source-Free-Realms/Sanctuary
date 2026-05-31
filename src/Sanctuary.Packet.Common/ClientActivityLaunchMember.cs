using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientActivityLaunchMember : ISerializableType
{
    public int Id;

    public ulong Guid;

    public string? Name;

    //   0x2 - Ready
    //   0x4 - Declined
    //   0x8 - Accepted
    //  0x10 - Invited
    //  0x20 - FailedValidation
    //  0x40 - Validated
    //  0x80 - Validating
    public byte InviteStatus;

    public bool IsFoundingMember;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(Guid);

        writer.Write(Name);

        writer.Write(InviteStatus);
        writer.Write(IsFoundingMember);
    }
}