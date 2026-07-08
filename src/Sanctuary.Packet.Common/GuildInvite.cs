using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildInvite : ISerializableType
{
    public ulong FromPlayerGuid;

    // unused
    public ulong Unknown;

    public ulong InviterPlayerGuid;

    // unused
    public int Unknown2;

    // unused
    public ulong Unknown3;

    public NameData InviterName = new();

    // unused
    public NameData InviteeName = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(FromPlayerGuid);

        writer.Write(Unknown);

        writer.Write(InviterPlayerGuid);

        writer.Write(Unknown2);
        writer.Write(Unknown3);

        InviterName.Serialize(writer);
        InviteeName.Serialize(writer);
    }
}
