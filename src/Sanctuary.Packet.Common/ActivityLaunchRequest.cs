using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ActivityLaunchRequest : ISerializableType
{
    public ulong RequestorGuid;

    public uint SysHashkey;
    public int SysDefId;

    public bool MembersOnly;

    public int ReqId;

    public int MinMembers;
    public int MaxMembers;

    public bool CanAddPlayers;
    public bool FoundersAutoAccept;
    public bool MembersInstantlyReady;
    public bool NonFoundingMembersInstantlyReady;

    public int ImageSetId;

    public int NameStringId;
    public int DescStringId;

    public byte[] SysSpecificData = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(RequestorGuid);

        writer.Write(SysHashkey);
        writer.Write(SysDefId);

        writer.Write(MembersOnly);

        writer.Write(ReqId);

        writer.Write(MinMembers);
        writer.Write(MaxMembers);

        writer.Write(CanAddPlayers);
        writer.Write(FoundersAutoAccept);
        writer.Write(MembersInstantlyReady);
        writer.Write(NonFoundingMembersInstantlyReady);

        writer.Write(ImageSetId);

        writer.Write(NameStringId);
        writer.Write(DescStringId);

        writer.WritePayload(SysSpecificData);
    }
}