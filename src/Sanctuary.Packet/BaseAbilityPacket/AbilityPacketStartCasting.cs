using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Targets;

namespace Sanctuary.Packet;

public class AbilityPacketStartCasting : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public ulong CasterGuid;

    public ulong Unused;

    public int CompositeEffectId;

    public int Animation;

    public int AbilityId;

    public float ActionTime;
    public bool HasActionProgress;


    public AbilityPacketStartCasting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(CasterGuid);
        writer.Write(Unused);
        writer.Write(CompositeEffectId);
        writer.Write(Animation);
        writer.Write(AbilityId);
        writer.Write(ActionTime);
        writer.Write(HasActionProgress);

        return writer.Buffer;
    }
}
