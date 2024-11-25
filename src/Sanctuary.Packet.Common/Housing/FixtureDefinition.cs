using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FixtureDefinition : ISerializableType
{
    public int Id;
    public int ItemDefinitionId;

    public int Unknown3;
    public int Unknown4;

    public string? Unknown11;
    public string? Unknown12;

    public bool Unknown5;
    public bool Unknown6;
    public bool Unknown7;
    public bool Unknown8;
    public bool Unknown9;
    public bool Unknown10;

    public Dictionary<int, FixtureAction> Actions = new();

    public class FixtureAction : ISerializableType
    {
        public int Unknown;
        public string? Unknown2;
        public int Unknown3;
        public int Unknown4;
        public int UnknownKey;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Unknown);
            writer.Write(Unknown2);
            writer.Write(Unknown3);
            writer.Write(Unknown4);
        }
    }

    public int CompositeEffectId;
    public float Unknown14;
    public float Unknown15;

    public bool Unknown16;
    public bool Unknown17;
    public bool Unknown18;
    public bool Unknown19;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(ItemDefinitionId);

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

        writer.Write(Actions);

        writer.Write(CompositeEffectId);
        writer.Write(Unknown14);
        writer.Write(Unknown15);

        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
    }
}