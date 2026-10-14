using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class EffectDefinition : ISerializableType
{
    public int Id;
    public int Type;
    public int Unknown3;
    public int Unknown4;
    public int ChanceToOccur;
    public bool Unknown6;
    public int Unknown7;
    public int Unknown8;
    public float Param1;
    public float Param2;
    public float Param3;
    public float Unknown40;
    public float Unknown41;
    public string? Unknown20;
    public int Unknown12;
    public int Unknown13;
    public int Unknown14;
    public int Unknown15;
    public int MaxCharges;
    public int Unknown17;
    public bool Unknown18;
    public bool Unknown19;
    public int Param1TunedLow;
    public float Param1TunedHigh;
    public int Param2TunedLow;
    public float Param2TunedHigh;
    public float Param3TunedLow;
    public float Param3TunedHigh;
    public int ToolTipStringId;
    public int Unknown28;
    public int Unknown29;
    public float Unknown30;
    public int Unknown31;
    public int DurationSeconds;
    public int Unknown33;
    public int Unknown34;
    public int Unknown35;
    public int Unknown36;
    public int Unknown37;
    public int Unknown38;
    public bool Unknown39;
    public float Unknown42;
    public bool Unknown43;
    public int DisplayOrder;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Type);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(ChanceToOccur);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Param3);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(MaxCharges);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(Param1TunedLow);
        writer.Write(Param2TunedLow);
        writer.Write(Param3TunedLow);
        writer.Write(Param1TunedHigh);
        writer.Write(Param2TunedHigh);
        writer.Write(Param3TunedHigh);
        writer.Write(ToolTipStringId);
        writer.Write(Unknown28);
        writer.Write(Unknown29);
        writer.Write(Unknown30);
        writer.Write(Unknown31);
        writer.Write(DurationSeconds);
        writer.Write(Unknown33);
        writer.Write(Unknown34);
        writer.Write(Unknown35);
        writer.Write(Unknown36);
        writer.Write(Unknown37);
        writer.Write(Unknown38);
        writer.Write(Unknown39);
        writer.Write(Unknown40);
        writer.Write(Unknown41);
        writer.Write(Unknown42);
        writer.Write(Unknown43);
        writer.Write(DisplayOrder);
    }
}
