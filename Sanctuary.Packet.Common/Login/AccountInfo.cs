using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AccountInfo
{
    public bool IsMember;

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