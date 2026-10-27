using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AccountInfo
{
    // Membership no longer exists. The client still locks member content behind this flag, so everyone is a member.
    public bool IsMember = true;

    public int MaxCharacters;

    public bool IsAdminAccount;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(IsMember);
        writer.Write(MaxCharacters);
        writer.Write(IsAdminAccount);

        return writer.Buffer;
    }
}