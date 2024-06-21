using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PacketMountInfo : ISerializableType
{
    public int Id;

    public int NameId;

    public int ImageSetId;

    public int TintId;
    public string TintAlias = null!;

    public ulong Guid;

    public bool MembersOnly;

    public bool IsUpgradable;
    public bool IsUpgraded;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NameId);

        writer.Write(ImageSetId);

        writer.Write(Guid);

        writer.Write(MembersOnly);

        writer.Write(TintId);
        writer.Write(TintAlias);

        writer.Write(IsUpgradable);
        writer.Write(IsUpgraded);
    }
}