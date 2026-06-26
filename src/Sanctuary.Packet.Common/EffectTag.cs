using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class EffectTag : ISerializableType
{
    public int InstanceId;

    public int DefinitionId;

    public int Unknown3;

    public uint StartTime;
    public uint StopTime;

    public long Unknown9;
    public bool Unknown8;
    public int Unknown4;
    public int Unknown5;
    public int Unknown6;

    public int Duration;

    public int Unknown12;

    public int CompositeEffectId;

    public long Unknown14;
    public int Unknown15;
    public int Unknown16;
    public bool Unknown17;
    public bool Unknown18;
    public bool Unknown19;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(InstanceId);

        writer.Write(DefinitionId);

        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);

        writer.Write(Duration);

        writer.Write(Unknown8);
        writer.Write(Unknown9);

        writer.Write(StartTime);
        writer.Write(StopTime);

        writer.Write(Unknown12);

        writer.Write(CompositeEffectId);

        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
    }
}