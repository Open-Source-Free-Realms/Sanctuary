using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GuildMemberStatusUpdatePacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 17;

    public ulong GuildGuid;
    public ulong MemberGuid;

    public NameData Name = new();

    public int Role;

    public bool Online;

    /// <summary>
    /// 1 - You/Other Joined
    /// 2 - You/Other Removed
    /// 3 - You/Other Quit
    /// 4 - You/Other Promoted
    /// 5 - You/Other Demoted
    /// 6 - Online/Offline
    /// 7 - None
    /// </summary>
    public int Type;

    public int WorldId;

    public int Unknown;

    public int ProfileId;
    public int ProfileRank;

    public GuildMemberStatusUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(GuildGuid);
        writer.Write(MemberGuid);

        Name.Serialize(writer);

        writer.Write(Role);

        writer.Write(Online);

        writer.Write(Type);

        writer.Write(WorldId);

        writer.Write(Unknown);

        writer.Write(ProfileId);
        writer.Write(ProfileRank);

        return writer.Buffer;
    }
}