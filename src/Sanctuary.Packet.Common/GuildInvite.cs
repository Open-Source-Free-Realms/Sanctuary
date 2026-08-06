using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GuildInvite : ISerializableType
{
    public ulong FromPlayerGuid;

    /// <summary>Unused</summary>
    private ulong Unknown = default;

    public ulong InviterPlayerGuid;

    /// <summary>Unused</summary>
    private int Unknown2 = default;

    /// <summary>Unused</summary>
    private ulong Unknown3 = default;

    public NameData InviterName = new();

    /// <summary>Unused</summary>
    private NameData InviteeName = new();

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