using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientActivityLaunchPacketInviteDetails : ClientActivityLaunchBasePacket, ISerializablePacket
{
    public new const int OpCode = 1;

    public bool Unknown2;

    public ulong Guid;

    public string? Inviter;

    public int MergeId;

    public List<ClientActivityLaunchMember> Members = [];

    public ActivityLaunchRequest Request = new();

    public ClientActivityLaunchPacketInviteDetails(int activityId, int unknown) : base(OpCode, activityId, unknown)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown2);

        writer.Write(Guid);

        writer.Write(Inviter);

        writer.Write(MergeId);

        writer.Write(Members);

        Request.Serialize(writer);

        return writer.Buffer;
    }
}