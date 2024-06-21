using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ActionBarSlot : ISerializableType
{
    public bool IsEmpty = true;

    public int IconId;
    public int IconTintId;

    public int NameId;

    public int Unknown5;
    public int Unknown6;
    public int Unknown7;
    public int ManaCost;

    public bool Enabled;

    public int Unknown10;

    public int TotalRefreshTime;

    public int Unknown12;

    public int Quantity;

    public bool Unknown14;
    public int Unknown15;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(IsEmpty);

        writer.Write(IconId);
        writer.Write(IconTintId);

        writer.Write(NameId);

        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(ManaCost);

        writer.Write(Enabled);

        writer.Write(Unknown10);

        writer.Write(TotalRefreshTime);

        writer.Write(Unknown12);

        writer.Write(Quantity);

        writer.Write(Unknown14);
        writer.Write(Unknown15);
    }
}