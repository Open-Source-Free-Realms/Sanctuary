using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GroupInviteInfo : ISerializableType, IDeserializableType
{
    public ulong InviterGuid;
    public NameData InviterName = new();

    public ulong InviteeGuid;
    public NameData InviteeName = new();

    public int Unknown;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(InviterGuid);
        InviterName.Serialize(writer);

        writer.Write(InviteeGuid);
        InviteeName.Serialize(writer);

        writer.Write(Unknown);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out InviterGuid))
            return false;

        if (!InviterName.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out InviteeGuid))
            return false;

        if (!InviteeName.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out Unknown))
            return false;

        return true;
    }
}
