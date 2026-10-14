using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Targets;

namespace Sanctuary.Packet;

public class AbilityPacketLaunchAndLand : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public ulong Guid;

    public List<Target> Targets = [];

    public int Unknown3;
    public int Unknown6;
    public int Unknown2;
    public int Unknown4;
    public int Unknown5;

    public bool Unknown7;
    public bool Unknown8;

    public int Unknown9;
    public int Unknown10;
    public int Unknown11;

    public Vector4 TargetLocation;

    public int Unknown13;
    public int Unknown14;
    public int Unknown15;

    public ActionBarData ActionBar = new();

    public int Unknown17;

    public long TargetGuid;

    public bool Unknown19;

    public ProjectileParameters ProjectileParameters = new();


    public AbilityPacketLaunchAndLand() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(Guid);
        writer.Write(Targets);
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
        writer.Write(TargetLocation);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        ActionBar.Serialize(writer);
        writer.Write(Unknown17);
        writer.Write(TargetGuid);
        writer.Write(Unknown19);
        ProjectileParameters.Serialize(writer);

        return writer.Buffer;
    }
}
