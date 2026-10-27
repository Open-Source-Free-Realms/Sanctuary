using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientAbilityDefinition : ISerializableType
{
    public int Id;
    public bool Unknown2;
    public bool Unknown3;
    public int NameId;
    public int SubDescriptionId;
    public int Unknown6;
    public int CastSeconds;
    public int Unknown8;
    public int Unknown9;
    public int CompositeEffect;
    public int Unknown11;
    public int Unknown12;
    public int Unknown13;
    public int ManaCost;
    public int Unknown15;
    public int Unknown16;
    public float Unknown17;
    public float Unknown18;
    public int Unknown19;
    public int Unknown20;
    public int ManaCostPerSecond;
    public bool Unknown22;
    public int AuraDuration;
    public int Unknown24;
    public int MaxAoeTargets;
    public float Unknown26;
    public int Unknown27;
    public int DescriptionId;
    public float Unknown29;
    public float Unknown30;
    public int Unknown31;
    public int Unknown32;
    public int Unknown33;
    public float Unknown34;
    public float Unknown35;
    public bool Unknown36;
    public int Unknown37;
    public int Unknown38;
    public bool Unknown39;
    public bool Unknown40;
    public bool Unknown41;
    public int Unknown42;
    public float Unknown43;
    public bool Unknown44;

    public Dictionary<int, EffectDefinition> EffectDefinitions = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(NameId);
        writer.Write(SubDescriptionId);
        writer.Write(Unknown6);
        writer.Write(CastSeconds);
        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(CompositeEffect);
        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(ManaCost);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(ManaCostPerSecond);
        writer.Write(Unknown22);
        writer.Write(AuraDuration);
        writer.Write(Unknown24);
        writer.Write(MaxAoeTargets);
        writer.Write(Unknown26);
        writer.Write(Unknown27);
        writer.Write(DescriptionId);
        writer.Write(Unknown29);
        writer.Write(Unknown30);
        writer.Write(Unknown31);
        writer.Write(Unknown32);
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
        writer.Write(EffectDefinitions);
        writer.Write(Unknown44);
    }
}
