using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class RewardBundleBase : ISerializableType
{
    public int Unknown3;
    public int Unknown4;
    public int Unknown2;
    public int Unknown5;
    public int Unknown15;
    public int Unknown9;
    public int Unknown10;

    public int IconId;

    public int NameId;

    public int Unknown6;
    public int Unknown7;
    public bool Unknown;
    public int Unknown8;
    public long Unknown11;
    public long Unknown12;

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);

        writer.Write(IconId);

        writer.Write(NameId);

        // TODO Entries
        writer.Write(0);

        writer.Write(Unknown15);
    }
}